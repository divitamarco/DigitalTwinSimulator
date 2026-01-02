using UnityEngine;
using UnityEngine.EventSystems;

public class OVRUIInput : MonoBehaviour
{
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
        {
            // Simula click sull'oggetto UI attualmente selezionato
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, pointer, ExecuteEvents.submitHandler);
        }
    }
}
