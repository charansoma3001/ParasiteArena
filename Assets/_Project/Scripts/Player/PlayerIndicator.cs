using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIndicator : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public float rotationSpeed = 180f;
    public float bounceHeight = 0.1f; 
    public float bounceSpeed = 8f; 

    void Update()
    {
        // bounce effect for the indicator
        float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;

        Vector3 newPos = target.position + offset;

        // apply bouce to y
        newPos.y += bounce;
        transform.position = newPos;

        // rotate the indicator in y axis 
        transform.Rotate(0f,rotationSpeed * Time.deltaTime, 0f);    
    }
}
