using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rope : MonoBehaviour
{
    public class Point
    {
        public bool Pin;
        public Vector3 Position;
        public Vector3 OldPosition;
    }

    public class Stick
    {
        public Point p0;
        public Point p1;
        public float Length = 1f;
    }

    public float Bounce = 0.9f;
    public float Gravity = -0.1f;
    [Range(0, 0.1f)]
    public float Friction = 0.99f;
    public float SprintLenghth = 3f;
    public int Iteration = 3;
    public float DampSmoothness = 8f;
    public float DampSmoothnessK = 0.01f;
    public int Density = 10;
    private List<Stick> Sticks;
    private List<Point> Points;
    private Transform[] PointTrans;
    public Transform HandleA;
    public Transform HandleB;
    // Start is called before the first frame update
    void Start()
    {
        Points = new List<Point>();
        Sticks = new List<Stick>();
        SprintLenghth = Vector3.Distance(HandleA.position, HandleB.position) / (float)Density;
        for (int i = 0; i < Density; i++)
        {
            var spawnPosition = Vector3.Lerp(HandleA.position, HandleB.position, (float)(i + 1) / (float)Density);
            var p = new Point
            {
                Position = spawnPosition,
                OldPosition = spawnPosition,
                Pin = i == 0 || i == Density - 1,
            };

            if (i != 0)
            {
                Sticks.Add(new Stick
                {
                    p1 = Points[Points.Count - 1],
                    p0 = p,
                    Length = SprintLenghth,
                });
            }
            Points.Add(p);
        }

        PointTrans = new Transform[Points.Count];
        for (int i = 0; i < Points.Count; i++)
        {
            PointTrans[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            PointTrans[i].position = Points[i].Position;
            PointTrans[i].localScale = Vector3.one * 0.25f;
            PointTrans[i].SetParent(transform);

            if (Points[i].Pin)
            {
                PointTrans[i].SetParent(i == 0 ? HandleA : HandleB);
            }
        }
    }

    private void UpdatePoints()
    {
        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            var v = (p.Position - p.OldPosition) * (1 - Friction);
            p.OldPosition = p.Position;
            p.Position += v;
            p.Position += new Vector3(0, Gravity, 0) * 0.02f;
            p.Position += -DampSmoothness * v * Time.deltaTime;
        }
    }

    private void ConstraintPoints()
    {
        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            // var v = (p.Position - p.OldPosition) * Friction;
            var cachedP = p.Position;

            if (p.Pin)
            {
                p.Position = PointTrans[i].position;
                p.OldPosition = p.Position;
            }
        }
    }

    private void UpdateSticks()
    {
        for (int i = 0; i < Sticks.Count; i++)
        {
            var s = Sticks[i];
            var v1 = (s.p0.Position - s.p0.OldPosition);
            var v2 = (s.p1.Position - s.p1.OldPosition);
            var relativeV1 = v1 - v2;
            var relativeV2 = v2 - v1;
            var d = s.p1.Position - s.p0.Position;
            var d2 = s.p0.Position - s.p1.Position;
            var distance = Vector3.Distance(s.p0.Position, s.p1.Position);


            var F1 = -DampSmoothnessK * (distance - SprintLenghth) * d2.normalized;
            s.p0.Position += (F1);
            s.p1.Position -= (F1);
        }
    }

    void Update()
    {
        UpdatePoints(); //for
        for (int i = 0; i < Iteration; i++)
        {
            UpdateSticks(); //for
            ConstraintPoints(); //for
        }
        //for
        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            PointTrans[i].position = p.Position;
        }
    }
}
