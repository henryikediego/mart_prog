using UnityEngine;

// Small reusable helper. Godot's Label3D has a built-in "billboard"
// property that always faces the camera; Unity's 3D TextMeshPro has
// no equivalent built in, so this component does the same job.
//
// Faces the camera fully now, using the camera's OWN up vector (cam.up)
// as the LookRotation reference instead of world Vector3.up. This is a
// fix for a real gap found during Phase 18 on-device testing: the
// original version constrained rotation to yaw-only, specifically to
// avoid Quaternion.LookRotation flipping when the look direction is
// nearly parallel to the up hint (the classic "looking straight down"
// degenerate case). That trade-off meant nothing ever fully faced the
// camera when viewed from a steep angle -- and looking down at a
// tabletop from above is exactly how an AR stack/array sitting on a
// surface normally gets viewed, so the skew was visible immediately.
//
// Using cam.up instead of world Vector3.up avoids the same degenerate
// case a different way, without giving up an axis: the direction toward
// the camera is essentially never parallel to the camera's OWN up
// vector (a camera looking straight down still has an up vector
// pointing roughly toward the horizon, not straight up), so this stays
// numerically stable at any real viewing angle.
public class Billboard : MonoBehaviour
{
    [Tooltip("Small fixed pitch-back (degrees) applied on top of the exact camera-facing " +
             "rotation, so cells/markers keep a slight 3D-card lean instead of reading as a " +
             "perfectly flat rectangle full-on to the camera. 0 = exactly face the camera. " +
             "Purely a stylistic touch (Phase 18 on-device finding) -- not physically accurate, " +
             "just enough to hint at the object's top edge. Flip the sign if it leans the wrong way.")]
    public float tiltDegrees = 10f;

    private Transform cam;

    private void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        Vector3 direction = cam.position - transform.position;

        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion faceCamera = Quaternion.LookRotation(-direction, cam.up);
        transform.rotation = faceCamera * Quaternion.Euler(tiltDegrees, 0f, 0f);
    }
}
