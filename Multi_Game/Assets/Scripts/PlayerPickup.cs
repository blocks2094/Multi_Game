using UnityEngine;

public class PlayerPickup : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private CharacterController playerController;

    [Header("감지 설정")]
    [SerializeField] private float pickupDistance = 3f;
    [SerializeField] private LayerMask pickupLayerMask = ~0;

    [Header("던지기 설정")]
    [SerializeField] private float throwForce = 10f;

    private PickableObject targetObject;
    private PickableObject heldObject;

    private void Update()
    {
        UpdateTargetObject();
        HandlePickupInput();
        HandleThrowInput();
    }

    private void UpdateTargetObject()
    {
        if (heldObject != null)
        {
            SetTargetObject(null);
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayerMask, QueryTriggerInteraction.Ignore))
        {
            SetTargetObject(null);
            return;
        }

        PickableObject detectedObject = hit.collider.GetComponentInParent<PickableObject>();

        if (detectedObject == null || detectedObject.IsHeld)
        {
            SetTargetObject(null);
            return;
        }

        SetTargetObject(detectedObject);
    }

    private void SetTargetObject(PickableObject newTargetObject)
    {
        if (targetObject == newTargetObject)
        {
            return;
        }

        if (targetObject != null)
        {
            targetObject.SetHighlighted(false);
        }

        targetObject = newTargetObject;

        if (targetObject != null)
        {
            targetObject.SetHighlighted(true);
        }
    }

    private void HandlePickupInput()
    {
        if (!Input.GetKeyDown(KeyCode.F))
        {
            return;
        }

        if (heldObject != null)
        {
            DropObject();
            return;
        }

        PickUpObject();
    }

    private void HandleThrowInput()
    {
        if (heldObject == null || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        ThrowObject();
    }

    private void PickUpObject()
    {
        if (targetObject == null)
        {
            return;
        }

        heldObject = targetObject;
        SetTargetObject(null);

        heldObject.PickUp(holdPoint, playerController);
    }

    private void DropObject()
    {
        heldObject.Drop(playerController);
        heldObject = null;
    }

    private void ThrowObject()
    {
        heldObject.Throw(playerController, playerCamera.transform.forward, throwForce);
        heldObject = null;
    }

    private void OnDisable()
    {
        SetTargetObject(null);

        if (heldObject != null)
        {
            DropObject();
        }
    }
}