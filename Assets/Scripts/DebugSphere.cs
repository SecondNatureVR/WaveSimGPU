using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugSphere : MonoBehaviour
{
    [SerializeField] public float radius;
    [SerializeField] public float speed;
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

        position = new Vector3(
            Mathf.Sin(theta) * Mathf.Cos(phi),
            Mathf.Sin(theta) * Mathf.Sin(phi),
            Mathf.Cos(theta)
        ) * radius;

        sphereRb.MovePosition(position);
    }
}
