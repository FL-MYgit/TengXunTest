using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Field : MonoBehaviour
{
    [SerializeField] private TMP_InputField field;
    [SerializeField] private NPCAgent agent;

    private void Awake()
    {
        field = GetComponent<TMP_InputField>();

        field.onEndEdit.AddListener(SendMessageTo);
    }

    public void SendMessageTo(string message)
    {
        if(message != null)
        {
            agent.SendMessageToAI(message);
            field.text = null;
        }
    }
}
