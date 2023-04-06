using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using UnityEngine.Jobs;

using math = Unity.Mathematics.math;
using random = Unity.Mathematics.Random;


public class FishGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform waterObject;
    public Transform objectPrefab;

    //Job Handling
    private PositionUpdateJob positionUpdateJob;
    private JobHandle positionUpdateJobHandle;


    [Header("Spawn Settings")]
    public int amountOfFish;
    public Vector3 spawnBounds;
    public float spawnHeight;
    public int swimChangeFrequency;

    [Header("Settings")]
    public float swimSpeed;
    public float turnSpeed;

    //The current velocities of each fish
    private NativeArray<Vector3> velocities;

    //TransformAccessArray allows us to pass (value type) transform information through to job.
    //Any modification made to the elements in the array will directly impact the scene transforms.
    private TransformAccessArray transformAccessArray;


    private void Start()
    {
        //Initialise velocities array with value for each fish
        velocities = new NativeArray<Vector3>(amountOfFish, Allocator.Persistent);

        //Initialise TransformAccessArrays with size based on amount of fish.
        transformAccessArray = new TransformAccessArray(amountOfFish);

        for (int i = 0; i < amountOfFish; i++)
        {
            float distanceX =
            Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2);

            float distanceZ =
            Random.Range(-spawnBounds.z / 2, spawnBounds.z / 2);

            // Set a random spawn point for each fish based on our Spawn Bounds
            Vector3 spawnPoint =
            (transform.position + Vector3.up * spawnHeight) + new Vector3(distanceX, 0, distanceZ);

            // Instantiate fish at spawn point.
            Transform t =
            (Transform)Instantiate(objectPrefab, spawnPoint,
            Quaternion.identity);

            // Add instantiated fish transform to our Transform Access Array
            transformAccessArray.Add(t);
        }

    }

    private void Update()
    {
        //Set up job with FishGenerator class variables
        //Seed is set using current millisecond to ensure a different seed is used for each call.
        positionUpdateJob = new PositionUpdateJob()
        {
            objectVelocities = velocities,
            jobDeltaTime = Time.deltaTime,
            swimSpeed = this.swimSpeed,
            turnSpeed = this.turnSpeed,
            time = Time.time,
            swimChangeFrequency = this.swimChangeFrequency,
            center = waterObject.position,
            bounds = spawnBounds,
            seed = System.DateTimeOffset.Now.Millisecond
        };

        //Schedule our job
        positionUpdateJobHandle = positionUpdateJob.Schedule(transformAccessArray);

    }

    private void LateUpdate()
    {
        //Ensure our job is completed
        positionUpdateJobHandle.Complete();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position + Vector3.up * spawnHeight, spawnBounds);
    }

    private void OnDestroy()
    {
        transformAccessArray.Dispose();
        velocities.Dispose();
    }

    [BurstCompile]
    struct PositionUpdateJob : IJobParallelForTransform
    {
        public NativeArray<Vector3> objectVelocities;

        public Vector3 bounds;
        public Vector3 center;

        public float jobDeltaTime;
        public float time;
        public float swimSpeed;
        public float turnSpeed;
        public int swimChangeFrequency;

        public float seed;

        public void Execute(int i, TransformAccess transform)
        {
            //Set current velocity of this fish
            Vector3 currentVelocity = objectVelocities[i];

            //Uses Unity's maths library to create a psuedorandom number generator that creates a seed by using the index and the system time.          
            random randomGen = new random((uint)(i * time + 1 + seed));

            //Moves the transform along it's own local forward direction, using localToWorldMatrix
            transform.position += transform.localToWorldMatrix.MultiplyVector(new Vector3(0, 0, 1)) *
            swimSpeed * jobDeltaTime * randomGen.NextFloat(0.3f, 1.0f);

            //Rotates the transform in the direction of currentVelocity
            if (currentVelocity != Vector3.zero)
            {
                transform.rotation =
                Quaternion.Lerp(transform.rotation,
                Quaternion.LookRotation(currentVelocity), turnSpeed * jobDeltaTime);
            }

            //Set current position of this fish
            Vector3 currentPosition = transform.position;

            bool randomise = true;

            //Check our position against our boundaries.
            //If it is outside of the boundary the velocity will be flipped towards the centre of the area.
            if (currentPosition.x > center.x + bounds.x / 2 ||
                currentPosition.x < center.x - bounds.x / 2 ||
                currentPosition.z > center.z + bounds.z / 2 ||
                currentPosition.z < center.z - bounds.z / 2)
            {
                Vector3 internalPosition = new Vector3(center.x +
                randomGen.NextFloat(-bounds.x / 2, bounds.x / 2) / 1.3f,
                0,
                center.z + randomGen.NextFloat(-bounds.z / 2, bounds.z / 2) / 1.3f);

                currentVelocity = (internalPosition - currentPosition).normalized;

                objectVelocities[i] = currentVelocity;

                transform.rotation = Quaternion.Lerp(transform.rotation,
                Quaternion.LookRotation(currentVelocity),
                turnSpeed * jobDeltaTime * 2);

                randomise = false;
            }

            //If the transform is within the boundaries, add a small chance that the direction will shift
            //Gives the fish more natural movement.
            if (randomise)
            {
                if (randomGen.NextInt(0, swimChangeFrequency) <= 2)
                {
                    objectVelocities[i] = new Vector3(randomGen.NextFloat(-1f, 1f),
                    0, randomGen.NextFloat(-1f, 1f));
                }
            }

        }
    }

}