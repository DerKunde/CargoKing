using System.Collections.Generic;
using CargoKing.Car;
using CargoKing.Input;
using CargoKing.Streets;
using CargoKing.Testing;
using CargoKing.Traffic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoKing.Driving
{
    /// <summary>
    /// Drives one car along a route on the street network. Holds route, position and goal, asks
    /// CargoKing.Traffic for steering, speed and gear, and hands one DrivingInput per physics step to
    /// the CarController. It wires; the arithmetic lives in CargoKing.Traffic.
    ///
    /// Stage 1 takes its goal from a mouse click; later stages hand it one from a director instead,
    /// through the same <see cref="SetGoal"/>.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class VehicleAgent : MonoBehaviour
    {
        /// <summary>How far from its route the car may be and still count as on it, metres.</summary>
        private const float ProjectionWindow = 5f;

        /// <summary>A click further than this from every street is not taken as a goal, metres.</summary>
        private const float MaximumGoalOffset = 20f;

        /// <summary>Within this distance of the goal, and standing, the car has arrived.</summary>
        private const float ArrivalDistance = 1.5f;

        /// <summary>Below this height the car has left the world, metres.</summary>
        private const float FallOutHeight = -50f;

        /// <summary>Taken over from AIDriver's defaults: leave reverse once the target clears the circle by this factor.</summary>
        private const float ReverseExitMargin = 1.15f;

        /// <summary>Taken over from AIDriver's defaults: give a reverse up after this long, seconds.</summary>
        private const float MaximumReverseDuration = 4f;

        /// <summary>Taken over from AIDriver's defaults: after a given-up reverse, forward for at least this long.</summary>
        private const float ReverseCooldown = 3f;

        /// <summary>Throttle on, below this speed for <see cref="StuckTime"/>: the car is stuck, m/s.</summary>
        private const float StuckSpeed = 0.5f;

        private const float StuckTime = 3f;

        /// <summary>Above this speed the car is making progress again and earlier attempts are forgiven, m/s.</summary>
        private const float ProgressSpeed = 2f;

        /// <summary>How long a recovery reverse backs up at least, seconds.</summary>
        private const float StuckReverseTime = 2f;

        private const int MaximumStuckAttempts = 2;

        private static readonly Color RouteColour = new Color(0.2f, 1f, 0.4f);
        private static readonly Color GoalColour = new Color(1f, 0.3f, 0.3f);
        private static readonly Color LookAheadColour = new Color(1f, 0.9f, 0.2f);

        private static DrivingProfile defaultProfile;

        [Tooltip("Network to drive on. Left empty, the first one in the scene is used.")]
        public StreetNetworkAuthoring network;

        [Tooltip("How this car drives. Left empty, built-in traffic defaults are used and a warning is logged.")]
        public DrivingProfile profile;

        private CarController car;
        private CarManeuvers maneuvers;
        private StreetNetworkRuntime runtime;
        private MouseToFloorPositioning goalProvider;

        /// <summary>The lanes still to drive, the one the car is on first.</summary>
        private readonly List<int> route = new List<int>();

        private StreetRoutePosition position = StreetRoutePosition.None;
        private StreetRoutePosition goal = StreetRoutePosition.None;
        private float lastShiftAt = float.NegativeInfinity;
        private Vector3 lookAheadPoint;
        private float stuckSince = -1f;
        private int stuckAttempts;

        public bool HasGoal => goal.IsValid;

        private void Awake()
        {
            car = GetComponent<CarController>();
            maneuvers = new CarManeuvers(car);

            if (profile == null)
            {
                if (defaultProfile == null)
                {
                    defaultProfile = ScriptableObject.CreateInstance<DrivingProfile>();
                    defaultProfile.hideFlags = HideFlags.DontSave;
                }

                profile = defaultProfile;
                Debug.LogWarning($"{name}: no DrivingProfile assigned, using built-in traffic defaults. Create one via Create > CargoKing > Driving Profile.", this);
            }

            if (network == null)
            {
                network = FindFirstObjectByType<StreetNetworkAuthoring>();
            }

            runtime = network != null ? network.Runtime : null;

            if (runtime == null || runtime.Asset.IsEmpty)
            {
                Debug.LogWarning($"{name}: no baked street network found. The agent switches itself off.", this);
                enabled = false;
                return;
            }

            goalProvider = FindFirstObjectByType<MouseToFloorPositioning>();
        }

        private void Update()
        {
            // Only a click sets a goal. MouseToFloorPositioning follows the pointer every step, and a
            // goal moving with the mouse would mean a new route search every step.
            if (goalProvider != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetGoal(goalProvider.carAITarget.Value);
            }
        }

        /// <summary>
        /// Drives to the place on the network nearest to a world point. A point far from every street is
        /// refused, and the car stays put.
        /// </summary>
        public void SetGoal(Vector3 point)
        {
            if (!enabled || !runtime.TryProject(point, out StreetRoutePosition found))
            {
                return;
            }

            StreetNetworkAsset asset = runtime.Asset;
            Vector3 onStreet = StreetLaneGeometry.SampleAt(asset.Samples, asset.Lanes[found.lane], found.distance).position;
            float offset = Vector3.Distance(onStreet, point);

            if (offset > MaximumGoalOffset)
            {
                Debug.LogWarning($"{name}: the goal is {offset:0} m from the nearest street, more than {MaximumGoalOffset:0} m. Staying put.", this);
                ClearGoal();
                return;
            }

            goal = found;
            Replan();
        }

        private void FixedUpdate()
        {
            if (car.transform.position.y < FallOutHeight)
            {
                Debug.LogWarning($"{name} fell out of the world at {car.transform.position}. The agent switches itself off.", this);
                enabled = false;
                return;
            }

            if (!goal.IsValid || !FollowRoute())
            {
                car.Drive(Hold());
                return;
            }

            float speed = car.CarSpeedInMS();

            if (HasArrived(speed))
            {
                ClearGoal();
                car.Drive(Hold());
                return;
            }

            // Steering first: it also places the look-ahead point the manoeuvres test against.
            float steer = SteerTowardsLookAhead(speed);

            switch (maneuvers.Advance(lookAheadPoint, ReverseExitMargin, MaximumReverseDuration, ReverseCooldown))
            {
                case ManeuverState.StoppingToReverse:
                    car.Drive(maneuvers.StopForGearChange(CarManeuvers.ReverseGear));
                    return;
                case ManeuverState.Reversing:
                    car.Drive(maneuvers.DriveInReverse(steer, profile.reverseThrottle));
                    return;
                case ManeuverState.StoppingToForward:
                    car.Drive(maneuvers.StopForGearChange(CarManeuvers.FirstGear));
                    return;
            }

            DrivingInput input = DriveForward(speed, steer);
            WatchForBeingStuck(input, speed);
            car.Drive(input);
        }

        /// <summary>
        /// Throttle on and no progress for a while: back up. Two attempts that do not free the car end
        /// the drive with a warning rather than hammering the same wall forever.
        /// </summary>
        private void WatchForBeingStuck(in DrivingInput input, float speed)
        {
            if (speed > ProgressSpeed)
            {
                stuckSince = -1f;
                stuckAttempts = 0;
                return;
            }

            // The launch holds the clutch while it ramps, and the gear policy holds it when slow: neither
            // is a car pushing against something.
            if (input.Throttle <= 0f || input.Clutch || speed > StuckSpeed)
            {
                stuckSince = -1f;
                return;
            }

            if (stuckSince < 0f)
            {
                stuckSince = Time.time;
                return;
            }

            if (Time.time - stuckSince < StuckTime)
            {
                return;
            }

            stuckSince = -1f;
            stuckAttempts++;

            if (stuckAttempts > MaximumStuckAttempts)
            {
                Debug.LogWarning($"{name} is stuck at {car.transform.position} and backing up has not helped twice. Stopping.", this);
                stuckAttempts = 0;
                ClearGoal();
                return;
            }

            maneuvers.StartReverse(StuckReverseTime);
        }

        /// <summary>
        /// Finds the car on its route and keeps the route's first lane the one it is on. Falls back to a
        /// network-wide projection and a new route when the car has left its route.
        /// </summary>
        /// <returns>False when the car has no route any more.</returns>
        private bool FollowRoute()
        {
            if (TryProjectOnRoute(car.transform.position, out int index, out StreetRoutePosition found))
            {
                position = found;
                route.RemoveRange(0, index);
            }
            else if (!Replan())
            {
                return false;
            }

            // Past the goal on its own lane: go round again rather than stand behind it forever.
            if (route.Count == 1 && position.distance > goal.distance + ArrivalDistance)
            {
                return Replan();
            }

            return true;
        }

        /// <summary>
        /// Closest place on the lane the car is on or the next one of its route, within the window. Only
        /// those two: at a junction entry every path from the same arm starts at the same point, and a
        /// wider search would jump to a sibling path the route does not take.
        /// </summary>
        /// <param name="index">0 when still on the first lane of the route, 1 when on the next.</param>
        private bool TryProjectOnRoute(Vector3 point, out int index, out StreetRoutePosition found)
        {
            index = -1;
            found = StreetRoutePosition.None;

            StreetNetworkAsset asset = runtime.Asset;
            float best = ProjectionWindow * ProjectionWindow;

            for (int candidate = 0; candidate < Mathf.Min(2, route.Count); candidate++)
            {
                int lane = route[candidate];
                float squared = StreetLaneGeometry.ClosestPoint(asset.Samples, asset.Lanes[lane], point, out float along);

                if (squared < best)
                {
                    best = squared;
                    index = candidate;
                    found = new StreetRoutePosition { lane = lane, distance = along };
                }
            }

            return index >= 0;
        }

        /// <summary>Finds the car anywhere on the network and a route from there to the goal.</summary>
        private bool Replan()
        {
            if (!runtime.TryProject(car.transform.position, out position)
                || !runtime.TryFindRoute(position, goal, route))
            {
                Debug.LogWarning($"{name}: no route from lane {position.lane} to lane {goal.lane}. Stopping.", this);
                ClearGoal();
                return false;
            }

            return true;
        }

        private bool HasArrived(float speed)
        {
            return route.Count == 1
                && Mathf.Abs(goal.distance - position.distance) <= ArrivalDistance
                && speed < ShiftPolicy.StandstillSpeed;
        }

        private void ClearGoal()
        {
            goal = StreetRoutePosition.None;
            route.Clear();
        }

        /// <summary>Pure pursuit towards a point on the route ahead, further ahead the faster the car goes.</summary>
        private float SteerTowardsLookAhead(float speed)
        {
            float distance = DrivingMath.LookAheadDistance(
                speed, profile.lookAheadTime, profile.minimumLookAhead, profile.maximumLookAhead);

            if (!runtime.TrySampleAhead(route, position, distance, out StreetNetworkSample sample))
            {
                return 0f;
            }

            lookAheadPoint = sample.position;

            Vector3 local = car.transform.InverseTransformDirection(lookAheadPoint - maneuvers.RearAxleCenter);
            float angle = DrivingMath.PurePursuitSteerAngle(local, maneuvers.Wheelbase);

            return Mathf.Clamp(angle / car.maxSteerAngle, -1f, 1f);
        }

        /// <summary>One step of driving forward along the route: speed plan, pedals, gear.</summary>
        private DrivingInput DriveForward(float speed, float steer)
        {
            CarEngine engine = car.carEngine;

            if (engine.engineStalled)
            {
                return new DrivingInput(steer, 0f, 1f, false, GearShift.None, true, true);
            }

            float deceleration = Mathf.Min(profile.comfortDeceleration, car.MaxBrakeDecelartion());
            float distanceToGoal = SpeedPlanner.DistanceToGoal(runtime.Asset, route, position, goal.distance);
            float desired = SpeedPlanner.AllowedSpeed(runtime, route, position, speed, distanceToGoal, profile, deceleration);

            bool wantsToMove = desired > ShiftPolicy.StandstillSpeed;

            if (!wantsToMove && speed < ShiftPolicy.StandstillSpeed)
            {
                return Hold();
            }

            // The launch sequence only in first. Standing in a higher gear, the gear policy first takes
            // it down with the clutch held.
            if (wantsToMove
                && engine.currentGear == CarManeuvers.FirstGear
                && maneuvers.TryLaunch(steer, out DrivingInput launch))
            {
                return launch;
            }

            Pedals pedals = DrivingMath.PedalsFor(
                desired, speed, profile.speedDeadBand, profile.throttleGain, profile.brakeGain);

            ShiftDecision decision = ShiftPolicy.Decide(new ShiftSituation
            {
                rpm = Mathf.Abs(engine.gearboxRevolutions),
                gear = engine.currentGear,
                gearCount = engine.GearCount,
                speed = speed,
                accelerating = pedals.throttle > 0f,
                secondsSinceShift = Time.time - lastShiftAt,
            }, profile);

            GearShift shift = GearShift.None;
            if (decision.shift == ShiftCommand.Up)
            {
                shift = GearShift.Up;
            }
            else if (decision.shift == ShiftCommand.Down)
            {
                shift = GearShift.Down;
            }

            if (shift != GearShift.None)
            {
                lastShiftAt = Time.time;
            }

            return new DrivingInput(steer, pedals.throttle, pedals.brake, false, shift, decision.holdClutch, false);
        }

        /// <summary>Standing still: brake on, clutch in, so the engine keeps running.</summary>
        private static DrivingInput Hold()
        {
            return new DrivingInput(0f, 0f, 1f, false, GearShift.None, true, false);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || runtime == null || !goal.IsValid)
            {
                return;
            }

            StreetNetworkAsset asset = runtime.Asset;
            StreetNetworkSample[] samples = asset.Samples;

            Gizmos.color = RouteColour;
            for (int index = 0; index < route.Count; index++)
            {
                StreetNetworkLane lane = asset.Lanes[route[index]];
                for (int sample = 1; sample < lane.sampleCount; sample++)
                {
                    Gizmos.DrawLine(
                        samples[lane.firstSample + sample - 1].position,
                        samples[lane.firstSample + sample].position);
                }
            }

            Gizmos.color = GoalColour;
            Gizmos.DrawWireSphere(StreetLaneGeometry.SampleAt(samples, asset.Lanes[goal.lane], goal.distance).position, 1f);

            Gizmos.color = LookAheadColour;
            Gizmos.DrawSphere(lookAheadPoint, 0.4f);
        }
    }
}
