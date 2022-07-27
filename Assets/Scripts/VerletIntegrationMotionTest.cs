using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;

public class VerletIntegrationMotionTest : MonoBehaviour
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
        //Point1的index
        public int Index0;
        //Point2的index
        public int Index1;
        public float RestLength;
    }

    private List<Point> ManagedPoints;
    public List<StickConstrain> ManagedSticks;

    private Transform[] PointTrans;

    void Start()
    {
        PointTrans = new Transform[2];
        ManagedPoints = new List<Point>();
        ManagedSticks = new List<StickConstrain>();
        for (int i = 0; i < 2; i++)
        {
            ManagedPoints.Add(new Point

            {
                Position = Vector3.zero,
                OldPosition = new Vector3(0.05f * (i + 2), 0.03f * i + 0.01f, 0)
            });
            PointTrans[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        }
        ManagedSticks.Add(new StickConstrain
        {
            Index0 = 0,
            Index1 = 1,
            RestLength = 2f
        });

    }

    private void UpdatePoints()
    {
        for (int i = 0; i < ManagedPoints.Count; i++)
        {
            var p = ManagedPoints[i];
            var v = (p.Position - p.OldPosition);
            p.OldPosition = p.Position;
            p.Position += v;
            p.Position += new Vector3(0, -0.5f, 0) * 0.02f;

            //handle collision
            if (p.Position.x > 10 || p.Position.x < -10)
            {
                if (p.Position.x > 0)
                {
                    p.Position.x = 10;
                }
                else
                {
                    p.Position.x = -10;
                }
                p.OldPosition.x = p.Position.x + v.x * 1f;
            }
            if (p.Position.y > 10 || p.Position.y < -10)
            {
                if (p.Position.y > 0)
                {
                    p.Position.y = 10;
                }
                else
                {
                    p.Position.y = -10;
                }
                p.OldPosition.y = p.Position.y + v.y * 1f;
            }
            ManagedPoints[i] = p;
        }
    }

    public Transform SphereCollider;

    private void UpdateColliderConstraint()
    {
        for (int i = 0; i < ManagedPoints.Count; i++)
        {
            var p = ManagedPoints[i];
            var cachedP = p.Position;

            //handle collision
            if (Vector3.Distance(p.Position, SphereCollider.position) <= SphereCollider.localScale.x * 0.5f)
            {
                var normal = (p.Position - SphereCollider.position).normalized;
                var positionOnSurface = SphereCollider.position + normal * (SphereCollider.localScale.x * 0.5f);
                p.Position = p.OldPosition;
                p.OldPosition = cachedP - 0.5f * Vector3.Distance(cachedP, positionOnSurface) * (cachedP - positionOnSurface).normalized;
            }

            ManagedPoints[i] = p;
        }
    }

    private void UpdateStickConstraint()
    {
        for (int i = 0; i < ManagedSticks.Count; i++)
        {
            var s = ManagedSticks[i];
            var p0 = ManagedPoints[s.Index0];
            var p1 = ManagedPoints[s.Index1];

            var d1 = p0.Position - p1.Position;
            var distance = Vector3.Distance(p0.Position, p1.Position);
            var difference = (s.RestLength - distance) / distance;
            var F1 = d1 * 0.5f * difference;
            p0.Position += (F1);
            p1.Position -= (F1);
            ManagedPoints[s.Index0] = p0;
            ManagedPoints[s.Index1] = p1;
        }
    }

    private void UpdatePinnedConstraint()
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

            ManagedPoints[i] = p;
        }
    }

    void FixedUpdate()
    {
        UpdatePoints();   //粒子的位移
        /*
        for (int j = 0; j < ManagedPoints.Count; j++)
        {
            //Motion Logic
        }
        */
        for (int i = 0; i < 3; i++)  //constraint的結果需要多次計算才能趨於穩定  
        {

            UpdateStickConstraint();
            /*
            for (int j = 0; j < ManagedSticks.Count; j++)
            {
                //constraint logic
            }
            */

            UpdatePinnedConstraint();
            /*
            for (int j = 0; j < ManagedPoints.Count; j++)
            {
                //constraint logic
            }
            */

            //UpdateColliderConstraint();
            /*
            for (int j = 0; j < ManagedPoints.Count; j++)
            {
                //constraint logic
            }*/
        }

        //for
        for (int i = 0; i < ManagedPoints.Count; i++)
        {
            var p = ManagedPoints[i];
            PointTrans[i].position = p.Position;
        }
    }
}
