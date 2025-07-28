using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Authorizer : MonoBehaviour
{
    GameObject suspendedObject;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        suspendedObject = new GameObject();
        #if UNITY_WEBPLAYER || UNITY_FLASH
            yield Application.RequestUserAuthorization(UserAuthorization.WebCam | UserAuthorization.Microphone);
        #endif
        
        suspendedObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
