using UnityEngine;

public class BuoyantObject : MonoBehaviour
{
    
    public Transform[] pontoons;

    private Rigidbody rb;
    // Start is called before the first frame update
    void Start()
    {
        // get rigidbody from object
        
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("BuoyantObject must have a Rigidbody component attached to it.");
            return;
        }

        // if pontoons are empty add a 0 buoyancy point
        if (pontoons.Length == 0)
        {
            pontoons = new Transform[1];
            pontoons[0] = transform;
        }


        CentralizedBuoyancyManager centralizedBuoyancyManager = FindObjectOfType<CentralizedBuoyancyManager>();
        // register this object with the CentralizedBuoyancyManager
        centralizedBuoyancyManager.RegisterBuoyantObject(rb, pontoons);

    }

    private void OnDestroy()
    {
        CentralizedBuoyancyManager centralizedBuoyancyManager = FindObjectOfType<CentralizedBuoyancyManager>();
        // unregister this object with the CentralizedBuoyancyManager
        centralizedBuoyancyManager.UnregisterBuoyantObject(rb);
    }

}
