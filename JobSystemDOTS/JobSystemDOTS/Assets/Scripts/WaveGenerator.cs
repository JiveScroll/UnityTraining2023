using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class WaveGenerator : MonoBehaviour
{
    [Header("Wave Parameters")]
    public float waveScale;
    public float waveOffsetSpeed;
    public float waveHeight;

    //Job handling
    private JobHandle meshModificationJobHandle; 
    private UpdateMeshJob meshModificationJob;

    [Header("References and Prefabs")]
    public MeshFilter waterMeshFilter;
    private Mesh waterMesh;

    NativeArray<Vector3> waterVertices;

    [ReadOnly]
    NativeArray<Vector3> waterNormals;


    private void Start()
    {
        waterMesh = waterMeshFilter.mesh;

        //MarkDynamic used to optimize sending vertex changes from CPU to GPU
        waterMesh.MarkDynamic();

        //Persistent allocator used to allow us to update the vertices for the lifetime of the program.
        //Otherwise we'd have to reinitialise the NativeArray each time the job finishes.
        waterVertices = new NativeArray<Vector3>(waterMesh.vertices, Allocator.Persistent);
        waterNormals = new NativeArray<Vector3>(waterMesh.normals, Allocator.Persistent);
    }

    private void Update()
    {
        //Initialize our UpdateMeshJob
        meshModificationJob = new UpdateMeshJob()
        {
            vertices = waterVertices,
            normals = waterNormals,
            offsetSpeed = waveOffsetSpeed,
            time = Time.time,
            scale = waveScale,
            height = waveHeight
        };

        //Schedule requires the length of the loop (Native Array size) and the batch size (determines how many segments the work will be divided into).
        meshModificationJobHandle =
        meshModificationJob.Schedule(waterVertices.Length, 64);
    }

    private void LateUpdate()
    {
        //Ensures the completion of the job.
        //You can't get the result of the vertices inside the job before it completes.
        meshModificationJobHandle.Complete();

        //Unity allows us to directly set the vertices of a mesh from the jobs vertices
        //Eliminates need to copy the data back and forth between threads
        waterMesh.SetVertices(meshModificationJob.vertices);

        //Recalculate normals based on new mesh data to ensure lighting interacts with the mesh correctly.
        waterMesh.RecalculateNormals();
    }

    /// <summary>
    /// Automatically ran by Unity when the game finishes or a component is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        //NativeContainers must be disposed of within the lifetime of their allocation
        //Since ours are persistent we can dispose them on OnDestroy()
        waterVertices.Dispose();
        waterNormals.Dispose();
    }

    [BurstCompile]
    private struct UpdateMeshJob : IJobParallelFor
    {
        //Public NativeArray used to read and write vertex data between job and main thread
        public NativeArray<Vector3> vertices;

        //Marked readonly to indicate we only want to read data from the main thread with this NativeArray
        [ReadOnly]
        public NativeArray<Vector3> normals;

        //Passed in by main thread, controls the noise function.
        public float offsetSpeed;
        public float scale;
        public float height;

        //Time is passed in because you can't access statics like Time.time within a job.
        public float time;

        public void Execute(int i)
        {
            //Only target vertices facing upwards
            if (normals[i].z > 0f)
            {
                var vertex = vertices[i];

                float noiseValue =
                Noise(vertex.x * scale + offsetSpeed * time, vertex.y * scale +
                offsetSpeed * time);

                //Apply noise value to vertex
                vertices[i] =
                new Vector3(vertex.x, vertex.y, noiseValue * height + 0.3f);
            }
        }

        private float Noise(float x, float y)
        {
            float2 pos = math.float2(x, y);
            return noise.snoise(pos);
        }
    }
}