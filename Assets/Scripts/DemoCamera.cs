using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DemoCamera : MonoBehaviour
{
    public UnityEvent OnPostRenderEvent;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnPostRender()
    {
        OnPostRenderEvent?.Invoke();
    }
}
