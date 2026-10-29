using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraDragController : MonoBehaviour
{
    public float dragSpeed = 2f;
    public float flySpeed = 5f;             // Speed for WASD camera movement
    public float zoomSpeed = 10f;
    public float minHeight = 5f;
    public float maxHeight = 50f;
    public float rotationSpeed = 2f;

    public Transform cameraTransform;       // Assign the Camera child transform in Inspector

    private Vector3 dragOrigin;
    private Plane dragPlane;

    void Update()
    {
        // --- Middle mouse drag movement ---
        if (Input.GetMouseButtonDown(2) && cameraTransform != null)
        {
            dragPlane = new Plane(cameraTransform.forward, cameraTransform.position + cameraTransform.forward * 10f);
            dragOrigin = GetMousePointOnPlane(dragPlane);
        }

        if (Input.GetMouseButton(2) && cameraTransform != null)
        {
            Vector3 currentPos = GetMousePointOnPlane(dragPlane);
            Vector3 difference = dragOrigin - currentPos;
            Vector3 newPosition = transform.position + difference;

            // Clamp Y to >= 2
            if (newPosition.y < 2f)
                newPosition.y = 2f;

            transform.position = newPosition;
        }

        // --- Scroll zoom ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && cameraTransform != null)
        {
            Vector3 zoomVector = cameraTransform.forward * scroll * zoomSpeed;
            Vector3 newPosition = transform.position + zoomVector;
            float newY = Mathf.Clamp(newPosition.y, minHeight, maxHeight);
            newPosition = new Vector3(newPosition.x, newY, newPosition.z);
            transform.position = newPosition;
        }

        // --- Right mouse camera rotation ---
        if (Input.GetMouseButton(1) && cameraTransform != null)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            Vector3 angles = cameraTransform.localEulerAngles;

            float pitch = angles.x - mouseY * rotationSpeed;
            if (pitch > 180) pitch -= 360;
            pitch = Mathf.Clamp(pitch, -32f, 89f);

            float yaw = angles.y + mouseX * rotationSpeed;

            cameraTransform.localEulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // --- WASD + QE movement while holding right mouse button ---
        if (cameraTransform != null)
        {
            Vector3 inputDirection = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) inputDirection += cameraTransform.forward;
            if (Input.GetKey(KeyCode.S)) inputDirection -= cameraTransform.forward;
            if (Input.GetKey(KeyCode.A)) inputDirection -= cameraTransform.right;
            if (Input.GetKey(KeyCode.D)) inputDirection += cameraTransform.right;
            if (Input.GetKey(KeyCode.E)) inputDirection += cameraTransform.up;
            if (Input.GetKey(KeyCode.Q)) inputDirection -= cameraTransform.up;

            Vector3 move = inputDirection.normalized * flySpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + move;

            // Clamp to y >= 2
            if (targetPosition.y >= 2f)
            {
                transform.position = targetPosition;
            }
            else
            {
                // Zero out downward movement if it would go below 2
                if (move.y < 0f)
                    move.y = 0f;

                // Recalculate the result with upward or flat movement only
                targetPosition = transform.position + move;

                // Still ensure final Y >= 2
                if (targetPosition.y >= 2f)
                    transform.position = targetPosition;
                else
                    transform.position = new Vector3(targetPosition.x, 2f, targetPosition.z);
            }
        }
    }

    private Vector3 GetMousePointOnPlane(Plane plane)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return transform.position;
    }
}
