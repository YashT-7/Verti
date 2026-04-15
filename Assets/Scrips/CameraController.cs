using UnityEngine;
using UnityEngine.InputSystem; //

public class CameraController : MonoBehaviour
{
    [Header("Speeds")]
    public float lookSpeed = 0.1f;
    public float zoomSpeed = 50f;
    public float panSpeed = 1.0f;

    private Vector2 rotation;

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        // 1. ZOOM (Q for In, E for Out)
        if (keyboard.qKey.isPressed)
            transform.position += transform.forward * (zoomSpeed * Time.deltaTime);
        if (keyboard.eKey.isPressed)
            transform.position -= transform.forward * (zoomSpeed * Time.deltaTime);

        // 2. SCROLL ZOOM (Optional addition for mouse wheel)
        float scrollValue = mouse.scroll.ReadValue().y;
        if (scrollValue != 0)
            transform.position += transform.forward * (scrollValue * 2.0f);

        // 3. PAN OR ORBIT (Right Click Logic)
        if (mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            // SHIFT + RIGHT CLICK = PAN
            if (keyboard.shiftKey.isPressed)
            {
                Vector3 pan = transform.right * (-mouseDelta.x * panSpeed) + transform.up * (-mouseDelta.y * panSpeed);
                transform.position += pan * Time.deltaTime * 10f;
            }
            // RIGHT CLICK ONLY = ORBIT/LOOK
            else
            {
                rotation.x += mouseDelta.x * lookSpeed;
                rotation.y -= mouseDelta.y * lookSpeed;
                transform.localRotation = Quaternion.Euler(rotation.y, rotation.x, 0);
            }
        }
    }
}