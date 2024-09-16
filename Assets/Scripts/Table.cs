using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Table : MonoBehaviour
{
	public GameObject table_top;
		
	public float width
    	{
		get { return table_top.transform.localScale.x; }
	}

	public float length
	{
		get { return 2*table_top.transform.localScale.z; }
	}
		
	public float height
	{
		get { return table_top.transform.localPosition.y + 0.5f*table_top.transform.localScale.y; }
	}
	public void show_table(bool show)
	{
		foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
		    r.enabled = show;
	}
}
