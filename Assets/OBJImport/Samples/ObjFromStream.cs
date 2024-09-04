using Dummiesman;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ObjFromStream : MonoBehaviour
{
    IEnumerator Start()
    {
        // Create UnityWebRequest
        UnityWebRequest www = UnityWebRequest.Get("https://people.sc.fsu.edu/~jburkardt/data/obj/lamp.obj");

        // Send request and wait for response
        yield return www.SendWebRequest();

        // Check for errors
        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Get data from response
            byte[] data = www.downloadHandler.data;

            // Create stream and load model
            using (var textStream = new MemoryStream(data))
            {
                var loadedObj = new OBJLoader().Load(textStream);
            }
        }
    }
}