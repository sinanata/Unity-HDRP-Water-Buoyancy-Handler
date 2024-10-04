using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Manages buoyancy for all buoyant objects and their pontoons
/// Utilizes the HDRP Water System and Unity's Job System for efficient buoyancy calculations.
/// </summary>
public class CentralizedBuoyancyManager : MonoBehaviour
{
    // ==============================
    // PUBLIC VARIABLES
    // ==============================

    [Header("Buoyancy Settings")]

    /// <summary>
    /// The depth before a pontoon is fully submerged.
    /// </summary>
    [Tooltip("The depth before a pontoon is fully submerged.")]
    public float depthBeforeSubmerged = 1f;

    /// <summary>
    /// The amount of displacement (buoyancy force) when fully submerged.
    /// </summary>
    [Tooltip("The amount of displacement (buoyancy force) when fully submerged.")]
    public float displacementAmount = 3f;

    /// <summary>
    /// The drag applied when submerged.
    /// </summary>
    [Tooltip("The drag applied when submerged.")]
    public float waterDrag = 0.99f;

    /// <summary>
    /// The angular drag applied when submerged.
    /// </summary>
    [Tooltip("The angular drag applied when submerged.")]
    public float waterAngularDrag = 0.5f;

    /// <summary>
    /// Reference to the WaterSurface component representing the water surface.
    /// </summary>
    [Tooltip("Reference to the WaterSurface component representing the water surface.")]
    public WaterSurface waterSurface;

    // ==============================
    // PRIVATE VARIABLES
    // ==============================

    /// <summary>
    /// Maximum number of pontoons supported.
    /// </summary>
    private const int MaxPontoons = 10000;

    /// <summary>
    /// List of all registered buoyant objects.
    /// </summary>
    private List<BuoyantObject> buoyantObjects = new List<BuoyantObject>();

    // NativeArrays for the Job System

    // Input job parameters
    private NativeArray<float3> targetPositionBuffer; // Positions of pontoons

    // Output job parameters
    private NativeArray<float> heightBuffer; // Water heights at pontoons
    private NativeArray<float> errorBuffer;
    private NativeArray<float3> candidatePositionBuffer;
    private NativeArray<int> stepCountBuffer;

    /// <summary>
    /// Array to keep track of which buoyant object each pontoon belongs to.
    /// </summary>
    private NativeArray<int> buoyantObjectIndices; // Index of the buoyant object for each pontoon

    /// <summary>
    /// Total number of pontoons currently processed.
    /// </summary>
    private int pontoonCount = 0;

    /// <summary>
    /// Water simulation data required for the water height calculations.
    /// </summary>
    private WaterSimSearchData simData;

    // ==============================
    // INNER CLASSES
    // ==============================

    /// <summary>
    /// Holds data for each registered buoyant object.
    /// </summary>
    private class BuoyantObject
    {
        public Rigidbody objectRigidbody; // The object's Rigidbody
        public Transform[] pontoons;      // The object's pontoons (buoyancy points)
    }

    // ==============================
    // UNITY METHODS
    // ==============================

    void Start()
    {
        // Allocate NativeArrays with maximum capacity
        targetPositionBuffer = new NativeArray<float3>(MaxPontoons, Allocator.Persistent);
        heightBuffer = new NativeArray<float>(MaxPontoons, Allocator.Persistent);
        errorBuffer = new NativeArray<float>(MaxPontoons, Allocator.Persistent);
        candidatePositionBuffer = new NativeArray<float3>(MaxPontoons, Allocator.Persistent);
        stepCountBuffer = new NativeArray<int>(MaxPontoons, Allocator.Persistent);
        buoyantObjectIndices = new NativeArray<int>(MaxPontoons, Allocator.Persistent);

        // Initialize simulation data
        simData = new WaterSimSearchData();
    }

    void OnDestroy()
    {
        // Dispose of NativeArrays to prevent memory leaks
        if (targetPositionBuffer.IsCreated) targetPositionBuffer.Dispose();
        if (heightBuffer.IsCreated) heightBuffer.Dispose();
        if (errorBuffer.IsCreated) errorBuffer.Dispose();
        if (candidatePositionBuffer.IsCreated) candidatePositionBuffer.Dispose();
        if (stepCountBuffer.IsCreated) stepCountBuffer.Dispose();
        if (buoyantObjectIndices.IsCreated) buoyantObjectIndices.Dispose();
    }

    void FixedUpdate()
    {
        // Ensure the WaterSurface component is assigned
        if (waterSurface == null)
        {
            Debug.LogError("CentralizedBuoyancyHandler: WaterSurface is not assigned.");
            return;
        }

        // Try to get the simulation data if available
        if (!waterSurface.FillWaterSearchData(ref simData))
        {
            Debug.LogError("CentralizedBuoyancyHandler: Failed to fill WaterSimSearchData.");
            return;
        }

        // Reset pontoon count
        pontoonCount = 0;

        // Collect pontoon positions and buoyant object indices
        for (int objectIndex = 0; objectIndex < buoyantObjects.Count; objectIndex++)
        {
            BuoyantObject buoyantObject = buoyantObjects[objectIndex];

            for (int i = 0; i < buoyantObject.pontoons.Length; i++)
            {
                if (pontoonCount >= MaxPontoons)
                {
                    Debug.LogWarning("CentralizedBuoyancyHandler: Exceeded maximum number of pontoons.");
                    break;
                }

                Transform pontoon = buoyantObject.pontoons[i];

                // Store pontoon position
                targetPositionBuffer[pontoonCount] = pontoon.position;

                // Store buoyant object index for later reference
                buoyantObjectIndices[pontoonCount] = objectIndex;

                pontoonCount++;
            }
        }

        // Prepare the WaterSimulationSearchJob
        WaterSimulationSearchJob searchJob = new WaterSimulationSearchJob
        {
            simSearchData = simData,
            targetPositionBuffer = targetPositionBuffer,
            startPositionBuffer = targetPositionBuffer,
            maxIterations = 8,
            error = 0.01f,
            heightBuffer = heightBuffer,
            errorBuffer = errorBuffer,
            candidateLocationBuffer = candidatePositionBuffer,
            stepCountBuffer = stepCountBuffer
        };

        // Schedule the job
        JobHandle handle = searchJob.Schedule(pontoonCount, 64);
        handle.Complete();

        // After the job completes, heightBuffer contains the water heights

        // Calculate the new position for each buoyant object based on the average height of its pontoons
        for (int objectIndex = 0; objectIndex < buoyantObjects.Count; objectIndex++)
        {
            BuoyantObject buoyantObject = buoyantObjects[objectIndex];
            float totalHeight = 0f;
            int pontoonsCount = buoyantObject.pontoons.Length;

            // Calculate average height from the water for all pontoons
            for (int i = 0; i < pontoonsCount; i++)
            {
                int pontoonIndex = objectIndex * pontoonsCount + i;
                totalHeight += heightBuffer[pontoonIndex];
            }

            float averageWaterHeight = totalHeight / pontoonsCount;

            // Set the object's height to match the average water height
            Vector3 newPosition = buoyantObject.objectRigidbody.transform.position;
            newPosition.y = averageWaterHeight;

            // Update the object's position
            buoyantObject.objectRigidbody.transform.position = newPosition;
        }
    }

    // ==============================
    // PUBLIC METHODS
    // ==============================

    /// <summary>
    /// Registers a buoyant object with the buoyancy handler.
    /// </summary>
    /// <param name="objectRigidbody">The object's Rigidbody.</param>
    /// <param name="pontoons">The object's pontoons (buoyancy points).</param>
    public void RegisterBuoyantObject(Rigidbody objectRigidbody, Transform[] pontoons)
    {
        BuoyantObject buoyantObject = new BuoyantObject
        {
            objectRigidbody = objectRigidbody,
            pontoons = pontoons
        };
        buoyantObjects.Add(buoyantObject);
    }

    /// <summary>
    /// Unregisters a buoyant object from the buoyancy handler.
    /// </summary>
    /// <param name="objectRigidbody">The object's Rigidbody.</param>
    public void UnregisterBuoyantObject(Rigidbody objectRigidbody)
    {
        buoyantObjects.RemoveAll(obj => obj.objectRigidbody == objectRigidbody);
    }

    /// <summary>
    /// Check if given object is registered
    /// </summary>
    public bool IsObjectRegistered(Rigidbody objectRigidbody)
    {
        return buoyantObjects.Exists(obj => obj.objectRigidbody == objectRigidbody);
    }
}
