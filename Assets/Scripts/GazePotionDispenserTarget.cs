using UnityEngine;

[AddComponentMenu("")]
public class GazePotionDispenserTarget : MonoBehaviour
{
    public GazePotionDispenser dispenser;

    private void Start()
    {
        if (dispenser == null) dispenser = GetComponentInParent<GazePotionDispenser>();
        if (dispenser == null)
            for (Transform parent = transform; parent != null; parent = parent.parent)
            {
                if (parent.name != "Brewing Table") continue;
                dispenser = parent.GetComponent<GazePotionDispenser>();
                if (dispenser == null) dispenser = parent.gameObject.AddComponent<GazePotionDispenser>();
                break;
            }
        // The table needs the controller; only its tap pieces should be gaze targets.
        if (dispenser != null && dispenser.transform == transform)
        {
            dispenser = null;
            Destroy(this);
        }
    }
}
