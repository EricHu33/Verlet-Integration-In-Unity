using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoPingPongMover : MonoBehaviour
{
    public Vector3 Movement;
    public float PingPongFrequency;
    private Vector3 m_originPos;
    private Vector3 m_currentPosOffset;
    // Start is called before the first frame update
    void Start()
    {
        m_originPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = m_originPos + Mathf.Sin(Time.time * PingPongFrequency) * Movement;
    }
}
