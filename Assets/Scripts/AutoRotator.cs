using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoRotator : MonoBehaviour
{
    public Vector3 Rotation;
    public bool PingPong;
    public float PingPongFrequency;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate((PingPong ? Mathf.Cos(Time.time * PingPongFrequency) : 1) * Rotation * Time.deltaTime);
    }
}
