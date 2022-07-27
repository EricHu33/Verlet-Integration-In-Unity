using System.Collections;
using System.Linq;

using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;

public class VerletIntegration : MonoBehaviour
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
    public float Bounce = 0.9f;
    public float Gravity = -9.8f;
    public int Iteration = 3;
    // public float DampSmoothness = 8f;
    [Range(0.1f, 0.8f)]
    public float StickDamp = 0.35f;
    [Range(-0.5f, 1f)]
    public float MaxStretch = 0.1f;
    [Range(0f, 1f)]
    public float RestoreStretchForce = 0.5f;
    [Range(0f, 0.1f)]
    public float Friction = 0.99f;
    public float CollideDamp = 1f;
    public int BatchSize = 1;
    public Material mat;
    public List<StickConstrain> Sticks;
    public NativeArray<StickConstrain> m_stickConstrains;
    public NativeArray<Point> m_points;
    public NativeArray<Vector3> m_vertices;
    private NativeArray<JobHandle> m_iteratetionHandles;
    private Vector3[] managedVertices;
    public MeshRenderer Mesh;
    private List<Point> Points;
    private Transform[] PointTrans;
    public Transform Sphere;
    private JobHandle m_updateManagedVerticesHandle;
    private JobHandle m_positionHandle;
    private JobHandle m_allconstrainHandle;

    void Start()
    {
        Points = new List<Point>();
        Sticks = new List<StickConstrain>();


        var mesh = Mesh.GetComponent<MeshFilter>().mesh;
        var verticesList = new List<Vector3>();
        mesh.GetVertices(verticesList);
        var localToWorldMatrix = Mesh.transform.localToWorldMatrix;
        var WorldToMatrix = Mesh.transform.worldToLocalMatrix;
        for (int i = 0; i < verticesList.Count; i++)
        {
            var p = new Point
            {
                Position = Mesh.transform.TransformPoint(verticesList[i]),
                OldPosition = Mesh.transform.TransformPoint(verticesList[i]),
                OriginPosition = Mesh.transform.TransformPoint(verticesList[i]),
                Pin = Mesh.transform.TransformPoint(verticesList[i]).y > 4.5f,
            };
            Points.Add(p);
        }
        managedVertices = verticesList.ToArray();
        var tirangles = mesh.GetTriangles(0);
        for (int i = 0; i < tirangles.Length; i += 3)
        {

            if (!Sticks.Any(x => (x.Index0 == tirangles[i] && x.Index1 == tirangles[i + 1]) || (x.Index0 == tirangles[i + 1] && x.Index1 == tirangles[i])))
            {
                Sticks.Add(new StickConstrain
                {
                    Index0 = tirangles[i],
                    Index1 = tirangles[i + 1],
                    RestLength = Vector3.Distance(Points[tirangles[i]].Position, Points[tirangles[i + 1]].Position),
                });
            }
            if (!Sticks.Any(x => (x.Index0 == tirangles[i + 1] && x.Index1 == tirangles[i + 2]) || (x.Index0 == tirangles[i + 2] && x.Index1 == tirangles[i + 1])))
            {
                Sticks.Add(new StickConstrain
                {
                    Index0 = tirangles[i + 1],
                    Index1 = tirangles[i + 2],
                    RestLength = Vector3.Distance(Points[tirangles[i + 1]].Position, Points[tirangles[i + 2]].Position),
                });
            }
            if (!Sticks.Any(x => (x.Index0 == tirangles[i + 2] && x.Index1 == tirangles[i + 0]) || (x.Index0 == tirangles[i + 0] && x.Index1 == tirangles[i + 2])))
            {
                Sticks.Add(new StickConstrain
                {
                    Index0 = tirangles[i + 2],
                    Index1 = tirangles[i + 0],
                    RestLength = Vector3.Distance(Points[tirangles[i + 2]].Position, Points[tirangles[i + 0]].Position),
                });
            }


        }

        m_points = new NativeArray<Point>(Points.Count, Allocator.Persistent);
        m_vertices = new NativeArray<Vector3>(Points.Count, Allocator.Persistent);
        m_stickConstrains = new NativeArray<StickConstrain>(Sticks.Count, Allocator.Persistent);
        m_stickConstrains.CopyFrom(Sticks.ToArray());
        m_points.CopyFrom(Points.ToArray());
        m_vertices.CopyFrom(verticesList.ToArray());

        m_iteratetionHandles = new NativeArray<JobHandle>(Iteration * 4, Allocator.Persistent);

    }

    [BurstCompile]
    private struct UpdatePointsOriginPositionJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3> Vertices;
        public NativeArray<Point> Points;
        public Matrix4x4 LocalToWorldMatrix;
        public void Execute(int index)
        {
            var p = Points[index];
            if (p.Pin)
                p.Position = LocalToWorldMatrix.MultiplyPoint(Vertices[index]);
            p.OriginPosition = LocalToWorldMatrix.MultiplyPoint(Vertices[index]);
            Points[index] = p;
        }
    }

    [BurstCompile]
    private struct UpdatePositionJob : IJobParallelFor
    {
        public NativeArray<Point> Points;
        public float Friction;
        public Vector3 Accel;
        public float deltaTime;
        public float MaxDistDiff;
        public void Execute(int index)
        {
            var p = Points[index];
            var diffScalar = (p.OldPosition - p.Position).normalized;
            var v = (p.Position - p.OldPosition) * (1 - Friction);
            p.OldPosition = p.Position;
            p.Position += v;
            p.Position += Accel * deltaTime;
            Points[index] = p;
        }
    }


    [BurstCompile]
    private struct UpdateDistanceConstrainJob : IJob
    {
        [ReadOnly]
        public NativeArray<StickConstrain> StickConstrains;
        public NativeArray<Point> Points;
        public float StickDamp;
        public float deltaTime;
        public void Execute()
        {
            for (int index = 0; index < StickConstrains.Length; index++)
            {
                var s = StickConstrains[index];
                var p0 = Points[s.Index0];
                var p1 = Points[s.Index1];

                var d1 = p0.Position - p1.Position;
                var d2 = p1.Position - p0.Position;

                var distance = Vector3.Distance(p0.Position, p1.Position);
                var F1 = -StickDamp * (distance - s.RestLength) * d2.normalized;
                p0.Position -= (F1);
                p1.Position += (F1);
                Points[s.Index0] = p0;
                Points[s.Index1] = p1;
            }
        }
    }

    [BurstCompile]
    private struct UpdateClampDistanceConstrainJob : IJob
    {
        [ReadOnly]
        public NativeArray<StickConstrain> StickConstrains;
        public NativeArray<Point> Points;
        public float StickDamp;
        public float deltaTime;
        public float MaxStretch;
        public float RestoreStretchForce;
        public void Execute()
        {
            for (int index = 0; index < StickConstrains.Length; index++)
            {
                var s = StickConstrains[index];
                var p0 = Points[s.Index0];
                var p1 = Points[s.Index1];

                var d1 = p0.Position - p1.Position;
                var d2 = p1.Position - p0.Position;
                var distance = Vector3.Distance(p0.Position, p1.Position);

                if (distance > s.RestLength * (1 + MaxStretch))
                {
                    var force = !p0.Pin && !p1.Pin ? RestoreStretchForce : RestoreStretchForce * 2;
                    if (!p0.Pin)
                    {
                        //p0.OldPosition -= d2.normalized * (distance - s.RestLength) * force;
                        p0.Position += d2.normalized * (distance - s.RestLength) * force;
                        Points[s.Index0] = p0;
                    }
                    if (!p1.Pin)
                    {
                        //p1.OldPosition -= d1.normalized * (distance - s.RestLength) * force;
                        p1.Position += d1.normalized * (distance - s.RestLength) * force;
                        Points[s.Index1] = p1;
                    }
                }
            }

        }
    }

    [BurstCompile]
    private struct UpdateConstrainJob : IJobParallelFor
    {
        public NativeArray<Point> Points;
        public bool UseFlattenMath;
        public void Execute(int index)
        {
            var p = Points[index];
            var cachedP = p.Position;

            if (p.Pin)
            {
                p.Position = p.OriginPosition;
                p.OldPosition = p.OriginPosition;
            }
            Points[index] = p;
        }
    }

    [BurstCompile]
    private struct UpdateColliderConstrainJob : IJobParallelFor
    {
        public NativeArray<Point> Points;
        public float CollideDamp;
        public Vector3 SphereCenter;
        public float SphereRadius;
        public bool UseFlattenMath;
        public void Execute(int index)
        {

            var p = Points[index];
            var cachedP = p.Position;

            if (Vector3.Distance(p.Position, SphereCenter) <= SphereRadius)
            {
                var normal = (p.Position - SphereCenter).normalized;
                var positionOnSurface = SphereCenter + normal * SphereRadius;
                p.Position = cachedP - CollideDamp * Vector3.Distance(cachedP, positionOnSurface) * (cachedP - positionOnSurface).normalized;
                p.OldPosition = cachedP + CollideDamp * Vector3.Distance(cachedP, positionOnSurface) * (cachedP - positionOnSurface).normalized;
            }
            Points[index] = p;

        }
    }

    [BurstCompile]
    private struct UpdateTransformJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<Point> Points;

        public void Execute(int index, TransformAccess transform)
        {
            transform.position = Points[index].Position;
        }
    }

    [BurstCompile]
    private struct UpdateVerticesJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Point> Points;

        [WriteOnly]
        public NativeArray<Vector3> Vertices;
        public Matrix4x4 WorldToLocalMatrix;
        public void Execute(int index)
        {
            Vertices[index] = WorldToLocalMatrix.MultiplyPoint(Points[index].Position);
        }
    }
    public float MaxDistDiff = 0.5f;
    void Update()
    {
        var updatePointsOriginPositionJob = new UpdatePointsOriginPositionJob
        {
            Vertices = m_vertices,
            Points = m_points,
            LocalToWorldMatrix = Mesh.transform.localToWorldMatrix
        };

        var updateOriginHandle = updatePointsOriginPositionJob.Schedule(m_vertices.Length, 1);

        var updatePos = new UpdatePositionJob
        {
            Points = m_points,
            Friction = this.Friction,
            Accel = new Vector3(0, Gravity, 0),
            deltaTime = 1f / 60f,
            MaxDistDiff = MaxDistDiff,
        };
        m_positionHandle = updatePos.Schedule(m_points.Length, BatchSize, updateOriginHandle);

        var updateConstrainJob = new UpdateConstrainJob
        {
            Points = m_points,
        };

        var updateColliderConstrainJob = new UpdateColliderConstrainJob
        {
            Points = m_points,
            CollideDamp = this.CollideDamp,
            SphereCenter = Sphere.position,
            SphereRadius = Sphere.localScale.x * 0.5f + 1f
        };
        var updateDistanceConstrainJob = new UpdateDistanceConstrainJob
        {
            StickConstrains = m_stickConstrains,
            Points = m_points,
            StickDamp = this.StickDamp,
            deltaTime = 1 / 60f
        };
        var updateClampDistanceJob = new UpdateClampDistanceConstrainJob
        {
            StickConstrains = m_stickConstrains,
            Points = m_points,
            StickDamp = this.StickDamp,
            deltaTime = 1 / 60f,
            MaxStretch = MaxStretch,
            RestoreStretchForce = RestoreStretchForce,
        };

        var index = 0;
        for (int i = 0; i < Iteration; i++)
        {
            m_iteratetionHandles[index] = updateColliderConstrainJob.Schedule(m_points.Length, BatchSize, i == 0 ? m_positionHandle : m_iteratetionHandles[index - 1]);
            index++;
            m_iteratetionHandles[index] = updateDistanceConstrainJob.Schedule(m_iteratetionHandles[index - 1]);
            index++;
            m_iteratetionHandles[index] = updateConstrainJob.Schedule(m_points.Length, BatchSize, m_iteratetionHandles[index - 1]);
            index++;
            m_iteratetionHandles[index] = updateClampDistanceJob.Schedule(m_iteratetionHandles[index - 1]);
            index++;
        }
        m_allconstrainHandle = m_iteratetionHandles[index - 1];

        var UpdateVerticesJob = new UpdateVerticesJob
        {
            Points = m_points,
            Vertices = m_vertices,
            WorldToLocalMatrix = Mesh.transform.worldToLocalMatrix
        };
        m_updateManagedVerticesHandle = UpdateVerticesJob.Schedule(m_vertices.Length, 1, m_allconstrainHandle);

    }
    public float moveSpeed = 20f;


    private void LateUpdate()
    {
        m_updateManagedVerticesHandle.Complete();
        m_vertices.CopyTo(managedVertices);
        Mesh.GetComponent<MeshFilter>().mesh.SetVertices(managedVertices);
        // Mesh.GetComponent<MeshFilter>().mesh.RecalculateNormals();
        if (Input.GetKey(KeyCode.V))
        {
            Mesh.transform.Rotate(new Vector3(0, 0, moveSpeed * 10) * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.B))
        {
            Mesh.transform.Rotate(new Vector3(0, 0, moveSpeed * -10) * Time.deltaTime);
        }
        Mesh.transform.position = new Vector3(Mesh.transform.position.x + Input.GetAxisRaw("Horizontal") * Time.deltaTime * moveSpeed, Mesh.transform.position.y, Mesh.transform.position.z + Input.GetAxisRaw("Vertical") * moveSpeed * Time.deltaTime);
    }

    private void OnDestroy()
    {
        m_stickConstrains.Dispose();
        m_points.Dispose();
        m_iteratetionHandles.Dispose();
        m_vertices.Dispose();
    }
    public bool DebugDraw;

    public void OnPostRender()
    {
        if (!DebugDraw)
            return;
        if (mat == null)
        {
            Debug.LogError("Please Assign a material on the inspector");
            return;
        }
        GL.PushMatrix();
        mat.SetPass(0);

        for (int i = 0; i < m_stickConstrains.Length; i++)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.green);
            GL.Vertex(m_points[m_stickConstrains[i].Index0].Position);
            GL.Vertex(m_points[m_stickConstrains[i].Index1].Position);
            GL.End();
        }

        GL.PopMatrix();
    }
}
