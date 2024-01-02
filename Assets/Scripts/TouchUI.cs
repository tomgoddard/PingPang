using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchUI : MonoBehaviour
{
	private void OnTriggerEnter(Collider touched)
	{
		Toggle t = touched.GetComponent<Toggle>();
		if (t != null)
			ExecuteEvents.Execute(t.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
		Button b = touched.GetComponent<Button>();
		if (b != null)
			ExecuteEvents.Execute(b.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }
}
