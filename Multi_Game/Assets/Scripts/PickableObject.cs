using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ObjectOutline))]
public class PickableObject : MonoBehaviour
{
    private Rigidbody objectRigidbody;
    private ObjectOutline objectOutline;
    private Collider[] objectColliders;
    private Transform originalParent;

    public bool IsHeld { get; private set; }

    private void Awake()
    {
        objectRigidbody = GetComponent<Rigidbody>();
        objectOutline = GetComponent<ObjectOutline>();
        objectColliders = GetComponentsInChildren<Collider>();
    }

    public void SetHighlighted(bool isHighlighted)
    {
        objectOutline.SetVisible(isHighlighted && !IsHeld);
    }

    public void PickUp(Transform holdPoint, Collider playerCollider)
    {
        if (IsHeld)
        {
            return;
        }

        IsHeld = true;
        originalParent = transform.parent;

        SetHighlighted(false);

        objectRigidbody.velocity = Vector3.zero;
        objectRigidbody.angularVelocity = Vector3.zero;
        objectRigidbody.useGravity = false;
        objectRigidbody.isKinematic = true;

        SetPlayerCollision(playerCollider, true);

        transform.SetParent(holdPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Drop(Collider playerCollider)
    {
        if (!IsHeld)
        {
            return;
        }

        Release(playerCollider);
    }

    public void Throw(Collider playerCollider, Vector3 direction, float throwForce)
    {
        if (!IsHeld)
        {
            return;
        }

        Release(playerCollider);
        objectRigidbody.AddForce(direction.normalized * throwForce, ForceMode.Impulse);
    }

    private void Release(Collider playerCollider)
    {
        IsHeld = false;

        transform.SetParent(originalParent, true);

        objectRigidbody.isKinematic = false;
        objectRigidbody.useGravity = true;

        SetPlayerCollision(playerCollider, false);
    }

    private void SetPlayerCollision(Collider playerCollider, bool ignoreCollision)
    {
        if (playerCollider == null)
        {
            return;
        }

        foreach (Collider objectCollider in objectColliders)
        {
            Physics.IgnoreCollision(objectCollider, playerCollider, ignoreCollision);
        }
    }
}