using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FunkyCode;

public class SeparatedRoomLight : MonoBehaviour
{
    private Light2D roomLight;
    private Light2D activeGlobalLight;
    private Light2D disabledGlobalLight;

    private void Start()
    {
        roomLight = GetComponent<Light2D>();
        activeGlobalLight = GameObject.Find("Global Light 2D On").GetComponent<Light2D>();
        disabledGlobalLight = GameObject.Find("Global Light 2D Off").GetComponent<Light2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            if (player.view.IsMine)
            {
                ControlRoomLightAsync(true);
                ControlGlobalLightAsync(false);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            if (player.view.IsMine)
            { 
                ControlGlobalLightAsync(true);
                ControlRoomLightAsync(false);
            }
        }
    }

    private void ControlRoomLightAsync(bool active)
    {
        // Direct control without delay
        roomLight.enabled = active;
    }

    private void ControlGlobalLightAsync(bool active)
    {
        // Direct control without delay
        activeGlobalLight.enabled = active;
        disabledGlobalLight.enabled = !active;
    }
}
