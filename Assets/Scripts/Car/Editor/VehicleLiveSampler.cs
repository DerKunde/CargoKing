using System;
using System.Collections.Generic;
using CargoKing.Diagnostics;
using R3;
using Reflex.Core;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Car.Editor
{
    /// <summary>One wheel in one physics step, as the live tire graphs need it.</summary>
    internal readonly struct WheelSample
    {
        public readonly float SlipRatio;
        public readonly float SlipAngle;

        /// <summary>Longitudinal force over wheel load: the friction coefficient actually used.</summary>
        public readonly float LongitudinalMu;

        /// <summary>Lateral force over wheel load.</summary>
        public readonly float LateralMu;

        public readonly bool Grounded;

        public WheelSample(float slipRatio, float slipAngle, float longitudinalMu, float lateralMu, bool grounded)
        {
            SlipRatio = slipRatio;
            SlipAngle = slipAngle;
            LongitudinalMu = longitudinalMu;
            LateralMu = lateralMu;
            Grounded = grounded;
        }
    }

    /// <summary>The engine in one physics step, as the live engine and gearbox graphs need it.</summary>
    internal readonly struct EngineSample
    {
        public readonly float Rpm;

        /// <summary>Combustion minus friction, N*m: on the full-load curve at full throttle, below it at part throttle.</summary>
        public readonly float NetTorque;

        /// <summary>What the clutch passed on, through gear, final drive and efficiency, N*m at the axle.</summary>
        public readonly float DriveTorque;

        public readonly float SpeedKmh;
        public readonly int Gear;

        public EngineSample(float rpm, float netTorque, float driveTorque, float speedKmh, int gear)
        {
            Rpm = rpm;
            NetTorque = netTorque;
            DriveTorque = driveTorque;
            SpeedKmh = speedKmh;
            Gear = gear;
        }
    }

    /// <summary>The last second of one wheel.</summary>
    internal sealed class WheelTrace
    {
        public readonly string Label;
        public readonly Suspension Wheel;
        public readonly Color Color;
        public readonly RingBuffer<WheelSample> Samples = new RingBuffer<WheelSample>(VehicleLiveSampler.TrailLength);

        public WheelTrace(string label, Suspension wheel, Color color)
        {
            Label = label;
            Wheel = wheel;
            Color = color;
        }
    }

    /// <summary>
    /// Reads the car the player is driving once per physics step, for the live points in the
    /// profile graphs. Editor only - the physics carries no extra code for it.
    ///
    /// Follows <see cref="CurrentCarProvider"/> from the Reflex root container (bound by
    /// RootInstaller on RootScope.prefab), so it switches along with HUD and camera when the
    /// player changes cars. Samples at R3's PostFixedUpdate, after the FixedUpdate scripts, so it
    /// sees CarController's finished step: slip from the last sub-step, forces averaged over the
    /// step, exactly as the debug fields hold them.
    /// </summary>
    [InitializeOnLoad]
    internal static class VehicleLiveSampler
    {
        /// <summary>Samples kept per series: one second at 50 Hz.</summary>
        public const int TrailLength = 50;

        private const string OutsidePlayMode = "Live points appear in Play Mode.";

        private static readonly List<WheelTrace> wheels = new List<WheelTrace>();
        private static readonly RingBuffer<EngineSample> engine = new RingBuffer<EngineSample>(TrailLength);
        private static IDisposable carSubscription;
        private static IDisposable sampling;

        static VehicleLiveSampler()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>The car being driven; null outside Play Mode or before one is set.</summary>
        public static CarController CurrentCar { get; private set; }

        /// <summary>Its wheels in slot order FL, FR, RL, RR (missing slots are left out).</summary>
        public static IReadOnlyList<WheelTrace> Wheels => wheels;

        public static RingBuffer<EngineSample> Engine => engine;

        /// <summary>Why there are no live points, for the graphs to say; null while sampling a car.</summary>
        public static string Status { get; private set; } = OutsidePlayMode;

        /// <summary>Raised whenever <see cref="CurrentCar"/> changes, including to null when Play Mode ends.</summary>
        public static event Action CurrentCarChanged;

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                StartSampling();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                StopSampling();
            }
        }

        private static void StartSampling()
        {
            Container root = Container.RootContainer;
            if (root == null || !root.HasBinding<CurrentCarProvider>())
            {
                Status = "No Reflex root container with a CurrentCarProvider - no live points.";
                return;
            }

            Status = "Waiting for the player's car.";
            sampling = Observable.EveryUpdate(UnityFrameProvider.PostFixedUpdate).Subscribe(_ => Sample());
            carSubscription = root.Resolve<CurrentCarProvider>().Current.Subscribe(car => SetCar(car));
        }

        private static void StopSampling()
        {
            carSubscription?.Dispose();
            sampling?.Dispose();
            carSubscription = null;
            sampling = null;
            SetCar(null);
            Status = OutsidePlayMode;
        }

        private static void SetCar(CarController car)
        {
            CurrentCar = car;
            wheels.Clear();
            engine.Clear();

            if (car != null)
            {
                AddWheel("FL", car.frontLeftWheel, 0);
                AddWheel("FR", car.frontRightWheel, 1);
                AddWheel("RL", car.rearLeftWheel, 2);
                AddWheel("RR", car.rearRightWheel, 3);
                Status = null;
            }
            else if (sampling != null)
            {
                Status = "Waiting for the player's car.";
            }

            CurrentCarChanged?.Invoke();
        }

        private static void AddWheel(string label, Transform slot, int colorIndex)
        {
            Suspension wheel = slot != null ? slot.GetComponent<Suspension>() : null;
            if (wheel != null)
            {
                wheels.Add(new WheelTrace(label, wheel, ProfileGraphs.WheelColors[colorIndex]));
            }
        }

        private static void Sample()
        {
            if (CurrentCar == null)
            {
                return;
            }

            foreach (WheelTrace trace in wheels)
            {
                Suspension wheel = trace.Wheel;
                if (wheel == null)
                {
                    continue;
                }

                float load = wheel.NormalLoad;
                bool grounded = wheel.isGrounded && load > 0f;
                trace.Samples.Add(new WheelSample(
                    wheel.slipRatio,
                    wheel.slipAngle,
                    grounded ? wheel.tireLongitudinalForce.magnitude / load : 0f,
                    grounded ? wheel.tireSlip.magnitude / load : 0f,
                    grounded));
            }

            CarEngine carEngine = CurrentCar.carEngine;
            if (carEngine != null && carEngine.engineProfile != null && carEngine.gearboxProfile != null)
            {
                engine.Add(new EngineSample(
                    carEngine.revolutionsPerMinute,
                    carEngine.debugCombustionTorque - carEngine.debugFrictionTorque,
                    Mathf.Abs(carEngine.debugClutchReactionTorque * carEngine.TotalRatio) * carEngine.Efficiency,
                    Mathf.Abs(carEngine.speedInKmH),
                    carEngine.currentGear));
            }
        }
    }
}
