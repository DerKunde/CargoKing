using Unity.Mathematics;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// The orientation of a road at one point on its spline: +Z along the tangent, +Y up, +X to the
    /// right.
    ///
    /// Shared by everything that sits on a street rather than beside it - the mesh cross section and
    /// the lane offsets have to use the same frame, or the road and the path the AI drives on would
    /// lean apart from each other.
    /// </summary>
    public static class StreetFrame
    {
        /// <summary>Below this a direction is treated as having no direction at all.</summary>
        private const float MinimumSquaredLength = 0.000001f;

        /// <summary>
        /// Builds the frame at a point.
        ///
        /// The spline's up vector is only roughly perpendicular to its tangent, so the frame is
        /// re-orthogonalised first. LookRotation would otherwise quietly accept the skew and shear
        /// everything placed in the frame.
        /// </summary>
        public static Quaternion At(float3 splineTangent, float3 splineUp)
        {
            Vector3 forward = new Vector3(splineTangent.x, splineTangent.y, splineTangent.z);
            Vector3 up = new Vector3(splineUp.x, splineUp.y, splineUp.z);

            if (forward.sqrMagnitude < MinimumSquaredLength)
            {
                forward = Vector3.forward;
            }

            if (up.sqrMagnitude < MinimumSquaredLength)
            {
                up = Vector3.up;
            }

            forward.Normalize();

            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < MinimumSquaredLength)
            {
                // Up and tangent are parallel - a vertical piece of road. Any perpendicular will do.
                right = Vector3.Cross(Vector3.up, forward);
            }

            if (right.sqrMagnitude < MinimumSquaredLength)
            {
                right = Vector3.right;
            }

            right.Normalize();
            up = Vector3.Cross(forward, right);

            return Quaternion.LookRotation(forward, up);
        }
    }
}
