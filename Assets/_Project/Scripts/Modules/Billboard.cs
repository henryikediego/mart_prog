using UnityEngine;

// Small reusable helper. Godot's Label3D has a built-in "billboard"
// property that always faces the camera; Unity's 3D TextMeshPro has
// no equivalent built in, so this component does the same job.
//
// Constrained to yaw only (rotating left/right to face the camera,
// never tilting/rolling) — an unconstrained LookRotation can flip
// the text's "up" direction when viewed from a steep angle, making
// letters render sideways or upside down.
public class Billboard : MonoBehaviour
{
    private Transform cam;

    private void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        Vector3 direction = cam.position - transform.position;
        direction.y = 0f; // ignore height difference — yaw only

        if (direction.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
    }
}
