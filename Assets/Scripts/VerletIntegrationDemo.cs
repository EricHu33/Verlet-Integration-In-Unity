using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;

public class VerletIntegrationDemo : MonoBehaviour
{
    public struct Point
    {
        public bool Pin;
        public Vector3 Position;
        public Vector3 OldPosition;
        public Vector3 OriginPosition;
    }

    public struct StickConstrain
    {
        public int Index0;
        public int Index1;
        public float RestLength;
    }

    public int Width = 10;
    public int Height = 10;
    public float Gravity = -9.8f;
    public float SprintLenghth = 3f;
    public int Iteration = 3;
    [Range(0.1f, 0.85f)]
    public float StickDamp = 0.35f;
    [Range(0, 0.1f)]
    public float Friction = 0.99f;
    public float CollideDamp = 1f;
    public Material mat;
    public List<StickConstrain> ManagedSticks;
    public NativeArray<StickConstrain> m_stickConstrains;
    public NativeArray<Point> m_points;
    public NativeArray<Vector3> m_transPositions;

    private List<Point> ManagedPoints;
    private Transform[] PointTrans;
    private TransformAccessArray PointTransformAccessArray;
    public Transform Sphere;
    private NativeArray<JobHandle> handles;
    void Start()
    {
        ManagedPoints = new List<Point>();
        ManagedSticks = new List<StickConstrain>();
        var pivot = transform.position;
        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                var p = new Point
                {
                    Position = pivot + new Vector3(j * SprintLenghth, transform.position.y, i * SprintLenghth),
                    OldPosition = pivot + new Vector3(j * SprintLenghth, transform.position.y, i * SprintLenghth),
                    OriginPosition = new Vector3(j * SprintLenghth, transform.position.y, i * SprintLenghth),
                    Pin = i == 0,
                };
                ManagedPoints.Add(p);
            }
        }
        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                if (j < Width - 1)
                {
                    ManagedSticks.Add(new StickConstrain
                    {
                        Index0 = i * Width + j,
                        Index1 = i * Width + j + 1,
                        RestLength = SprintLenghth,
                    });
                }

                if (i < Height - 1)
                {
                    ManagedSticks.Add(new StickConstrain
                    {
                        Index0 = i * Width + j,
                        Index1 = (i + 1) * Width + j,
                        RestLength = SprintLenghth,
                    });
                }
            }
        }
        m_points = new NativeArray<Point>(ManagedPoints.Count, Allocator.Persistent);
        m_stickConstrains = new NativeArray<StickConstrain>(ManagedSticks.Count, Allocator.Persistent);
        m_points.CopyFrom(ManagedPoints.ToArray());
        m_transPositions = new NativeArray<Vector3>(ManagedPoints.Count, Allocator.Persistent);
        m_stickConstrains.CopyFrom(ManagedSticks.ToArray());

        PointTrans = new Transform[ManagedPoints.Count];
        for (int i = 0; i < ManagedPoints.Count; i++)
        {
            PointTrans[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            PointTrans[i].GetComponent<MeshRenderer>().sharedMaterial = mat;
            PointTrans[i].transform.localScale = Vector3.one * 0.25f;
            PointTrans[i].position = ManagedPoints[i].Position;
            PointTrans[i].SetParent(transform);
            Destroy(PointTrans[i].gameObject.GetComponent<Collider>());
            //  PointTrans[i].gameObject.GetComponent<Renderer>().enabled = false;
        }
        PointTransformAccessArray = new TransformAccessArray(PointTrans);
        handles = new NativeArray<JobHandle>(Iteration * 2, Allocator.Persistent);

    }


    [BurstCompile]
    private struct CopyTransPositionJob : IJobParallelForTransform
    {
        public NativeArray<Vector3> TransPositions;
        public void Execute(int index, TransformAccess transform)
        {
            TransPositions[index] = transform.position;
        }
    }

    [BurstCompile]
    private struct UpdatePositionJob : IJobParallelFor
    {
        public NativeArray<Point> Points;
        public float Friction;
        public Vector3 Accel;
        public float deltaTime;
        public void Execute(int index)
        {
            var p = Points[index];
            var v = (p.Position - p.OldPosition) * (1 - Friction);
            p.OldPosition = p.Position;
            p.Position += v;
            p.Position += Accel * deltaTime;
            Points[index] = p;
        }
    }

    [BurstCompile]
    private struct HandleAllConstrainJob : IJob
    {
        public NativeArray<Point> Points;
        [ReadOnly]
        public NativeArray<Vector3> TransPositions;
        [ReadOnly]
        public NativeArray<StickConstrain> StickConstrains;
        public float StickDamp;
        public float CollideDamp;
        public Vector3 SphereCenter;
        public float SphereRadius;
        public int IterateCount;

        public void Execute()
        {
            for (int k = 0; k < IterateCount; k++)
            {
                for (int i = 0; i < StickConstrains.Length; i++)
                {
                    var s = StickConstrains[i];
                    var p0 = Points[s.Index0];
                    var p1 = Points[s.Index1];
                    var d = p1.Position - p0.Position;
                    var distance = Vector3.Distance(p0.Position, p1.Position);
                    var F1 = -StickDamp * (distance - s.RestLength) * d.normalized;
                    p0.Position -= (F1);
                    p1.Position += (F1);
                    Points[s.Index0] = p0;
                    Points[s.Index1] = p1;
                }

                for (int i = 0; i < Points.Length; i++)
                {
                    var p = Points[i];
                    var cachedP = p.Position;

                    if (p.Pin)
                    {
                        //  var cachedOld = p.OldPosition;
                        p.Position = TransPositions[i];
                        p.OldPosition = TransPositions[i];
                    }

                    //handle collision
                    if (Vector3.Distance(p.Position, SphereCenter) <= SphereRadius)
                    {
                        var normal = (p.Position - SphereCenter).normalized;
                        var positionOnSurface = SphereCenter + normal * SphereRadius;
                        p.Position = p.OldPosition;
                        p.OldPosition = cachedP - CollideDamp * Vector3.Distance(cachedP, positionOnSurface) * (cachedP - positionOnSurface).normalized;
                    }
                    Points[i] = p;
                }
            }
        }
    }

    [BurstCompile]
    private struct UpdateTransformJob : IJobParallelForTransform
    {

        public NativeArray<Point> Points;

        public void Execute(int index, TransformAccess transform)
        {
            transform.position = Points[index].Position;
        }
    }

    private void UpdatePoints()
    {
        for (int i = 0; i < ManagedPoints.Count; i++)
        {
            var p = ManagedPoints[i];
            var v = (p.Position - p.OldPosition) * (1 - Friction);
            p.OldPosition = p.Position;
            p.Position += v;
            p.Position += new Vector3(0, Gravity, 0) * 0.02f;
            ManagedPoints[i] = p;
        }
    }

    private void ConstrainPoints()
    {
        for (int i = 0; i < ManagedPoints.Count; i++)
        {
            var p = ManagedPoints[i];
            var cachedP = p.Position;

            if (p.Pin)
            {
                p.Position = PointTrans[i].position;
                p.OldPosition = p.Position;
            }

            //handle collision
            if (Vector3.Distance(p.Position, Sphere.position) <= Sphere.localScale.x * 0.5f + 0.1f)
            {
                var normal = (p.Position - Sphere.position).normalized;
                var positionOnSurface = Sphere.position + normal * (Sphere.localScale.x * 0.5f + 0.1f);
                p.Position = p.OldPosition;
                p.OldPosition = cachedP - CollideDamp * Vector3.Distance(cachedP, positionOnSurface) * (cachedP - positionOnSurface).normalized;
            }
            ManagedPoints[i] = p;
        }
    }

    private void UpdateSticks()
    {
        for (int i = 0; i < ManagedSticks.Count; i++)
        {
            var s = ManagedSticks[i];
            var p0 = ManagedPoints[s.Index0];
            var p1 = ManagedPoints[s.Index1];

            var d1 = p0.Position - p1.Position;
            var distance = Vector3.Distance(p0.Position, p1.Position);
            var difference = (s.RestLength - distance) / distance;
            var F1 = d1 * StickDamp * difference;
            p0.Position += (F1);
            p1.Position -= (F1);
            ManagedPoints[s.Index0] = p0;
            ManagedPoints[s.Index1] = p1;
        }
    }
    private JobHandle m_cosntrainJobHanldles;
    private JobHandle m_transUpdateHandle;
    public bool UseJob = false;
    public int BatchSize = 1;
    private JobHandle PositionHandle;
    private JobHandle AllConsHandle;
    private JobHandle lastJobHandle;
    void Update()
    {
        if (UseJob)
        {
            var copyPositionJob = new CopyTransPositionJob
            {
                TransPositions = m_transPositions,
            };
            var copyPositonHandle = copyPositionJob.Schedule(PointTransformAccessArray);

            var updatePos = new UpdatePositionJob
            {
                Points = m_points,
                Friction = this.Friction,
                Accel = new Vector3(0, Gravity, 0),
                deltaTime = Time.deltaTime
            };

            PositionHandle = updatePos.Schedule(m_points.Length, BatchSize, copyPositonHandle);
            var handleAllConstrainJob = new HandleAllConstrainJob
            {
                Points = m_points,
                TransPositions = m_transPositions,
                StickConstrains = m_stickConstrains,
                StickDamp = StickDamp,
                CollideDamp = CollideDamp,
                SphereCenter = Sphere.position,
                SphereRadius = Sphere.localScale.x * 0.5f + 0.1f,
                IterateCount = Iteration,
            };
            AllConsHandle = handleAllConstrainJob.Schedule(PositionHandle);
            var updateTrans = new UpdateTransformJob
            {
                Points = m_points,
            };
            m_transUpdateHandle = updateTrans.Schedule(PointTransformAccessArray, AllConsHandle);


        }
        else
        {
            UpdatePoints(); //for
            for (int i = 0; i < Iteration; i++)
            {
                UpdateSticks(); //for
                ConstrainPoints(); //for
            }
            //for
            for (int i = 0; i < ManagedPoints.Count; i++)
            {
                var p = ManagedPoints[i];
                PointTrans[i].position = p.Position;
            }
        }
    }

    private void LateUpdate()
    {
        if (UseJob)
        {
            m_transUpdateHandle.Complete();
            for (int i = 0; i < m_points.Length; i++)
            {
                var p = m_points[i];
                p.OriginPosition = PointTransformAccessArray[i].position;
                m_points[i] = p;
            }
        }
    }

    private void OnDestroy()
    {
        m_stickConstrains.Dispose();
        m_points.Dispose();
        m_transPositions.Dispose();
        handles.Dispose();
        PointTransformAccessArray.Dispose();
    }

    public void OnPostRender()
    {
        if (mat == null)
        {
            Debug.LogError("Please Assign a material on the inspector");
            return;
        }
        GL.PushMatrix();
        mat.SetPass(0);
        if (UseJob)
        {
            for (int i = 0; i < m_stickConstrains.Length; i++)
            {
                GL.Begin(GL.LINES);
                GL.Color(Color.green);
                GL.Vertex(m_points[m_stickConstrains[i].Index0].Position);
                GL.Vertex(m_points[m_stickConstrains[i].Index1].Position);
                GL.End();
            }
        }
        else
        {
            for (int i = 0; i < ManagedSticks.Count; i++)
            {
                GL.Begin(GL.LINES);
                GL.Color(Color.green);
                GL.Vertex(ManagedPoints[ManagedSticks[i].Index0].Position);
                GL.Vertex(ManagedPoints[ManagedSticks[i].Index1].Position);
                GL.End();
            }
        }


        GL.PopMatrix();
    }
}
