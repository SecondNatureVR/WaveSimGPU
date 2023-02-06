using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugSphere : MonoBehaviour
{
    [SerializeField] public float radius;
    [SerializeField] public float speed;
    [SerializeField] public float spinSpeed;
    [SerializeField] public float riseSpeed;
    [SerializeField] public Transform Sphere;
    private Rigidbody sphereRb;

    private void Start()
    {
        sphereRb = Sphere.GetComponent<Rigidbody>();
    }

    void Update()
    {
        Vector3 position;

        float theta = Time.time * speed;
        float phi = (theta - 0.54321f) * .9f;
        // spherical orbiting ish
        // position = new Vector3(
        //     Mathf.Sin(theta) * Mathf.Cos(phi),
        //     Mathf.Sin(theta) * Mathf.Sin(phi),
        //     Mathf.Cos(theta)
        // ) * radius;

        float animRadius = radius;
        float riseHeight = radius;
        position = new Vector3(
            Mathf.Cos(theta),
            0,
            Mathf.Sin(theta)
        ) * radius;

        //position += Vector3.down * riseHeight + Vector3.up * Mathf.Repeat(riseSpeed * theta, riseHeight * 2);

        sphereRb.MovePosition(position);
    }
}
