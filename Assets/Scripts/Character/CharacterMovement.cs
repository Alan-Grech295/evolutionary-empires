using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMovement : MonoBehaviour
{
    public float charSpeed = 7.5f;
    CharacterController controller;
    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 direction = transform.forward * Input.GetAxis("Vertical") * charSpeed;
        direction += transform.right * Input.GetAxis("Horizontal") * charSpeed;
        controller.SimpleMove(direction);
    }


}
