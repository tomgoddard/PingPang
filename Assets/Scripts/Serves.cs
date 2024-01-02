using System.Collections;
using System.Collections.Generic;
using System.IO;     // Use File
using System;         // Use Serializable
using UnityEngine;

public class Serves : MonoBehaviour {
    public List<Serve> serves = new List<Serve>();
    int random_serve_max;
    bool serves_changed;    // Whether serves should be saved.
    
    void Awake() {
        if (!load_serves())
	    add_fixed_serves();
        this.serves_changed = false;
    }

    void add_fixed_serves() {
        List<Serve> s = serves;
        Vector3 fhtp = new Vector3 (-0.5f, 0.8f, 2f); // Toss from forehand side.
        Vector3 bhtp = new Vector3 (0.5f, 0.8f, 2f); // Toss from backhand side.
        Vector3 tv = new Vector3 (0, 4, 0);
        float r = 0.02f;
        float volley_height = 0.8f;
        Toss vtoss = new Toss (fhtp, tv, volley_height);
        float hit_height = 1.0f;
        Toss fhtoss = new Toss (fhtp, tv, hit_height);
        Toss bhtoss = new Toss (bhtp, tv, hit_height);
        s.Add (new Serve ("Forehand long topspin", fhtoss, new Vector3 (2f, -1f, -7f), -5f/r));
        s.Add (new Serve ("Forehand long topspin line", bhtoss, new Vector3 (0f, -1f, -7f), -5f/r));
        s.Add (new Serve ("Backhand long topspin", bhtoss, new Vector3 (-2f, -1f, -7f), -5f/r));
        s.Add (new Serve ("Backhand long topspin line", fhtoss, new Vector3 (0f, -1f, -7f), -5f/r));
        s.Add (new Serve ("Forehand short backspin", fhtoss, new Vector3 (2f, -1.1f, -6f), 5f/r));
        s.Add (new Serve ("Forehand short backspin line", bhtoss, new Vector3 (0f, -1.2f, -6f), 5f/r));
        s.Add (new Serve ("Backhand short backspin", bhtoss, new Vector3 (-2f, -1.3f, -6f), 5f/r));
        s.Add (new Serve ("Backhand short backspin line",fhtoss, new Vector3 (0f, -1.2f, -6f), 5f/r));
        random_serve_max = s.Count;
        s.Add (new Serve ("Forehand topspin volley", vtoss, new Vector3 (2f, 3f, -6f), -5f/r));
        s.Add (new Serve ("Backhand topspin volley", vtoss, new Vector3 (0f, 3f, -6f), -5f/r));
        s.Add (new Serve ("Forehand backspin volley", vtoss, new Vector3 (2f, 3f, -6f), 5f/r));
        s.Add (new Serve ("Backhand backspin volley", vtoss, new Vector3 (0f, 3f, -5f), 5f/r));
    }

    void OnApplicationQuit()
    {
        if (serves_changed)
            save_serves();
    }

    Serve find_serve(string name)
    {
        if (name == "Random")
        {
            int i = UnityEngine.Random.Range (0, random_serve_max);
            return serves[i];
        }
        for (int i = 0 ; i < serves.Count ; ++i)
            if (serves[i].name == name)
                return serves[i];
        return null;
    }

    public void add_serve(Serve serve)
    {
        serves.Add(serve);
        serves_changed = true;
    }

    public void remove_serve(Serve serve)
    {
        if (serves.Remove(serve))
            serves_changed = true;
    }

    void save_serves()
    {
        string path = Path.Combine(Application.persistentDataPath,
				   "serves.json");
        SaveServes ss = new SaveServes(serves);
        string serves_data = JsonUtility.ToJson(ss);
        File.WriteAllText(path, serves_data);
        Debug.Log("Saved " + ss.serves.Length + " serves to " + path);
    }

    bool load_serves()
    {
        string path = Path.Combine(Application.persistentDataPath,
				   "serves.json");
        if (!File.Exists(path))
	{
	    Debug.Log("Could not load serves from " + path);
	    return false;
	}
        string serves_data = File.ReadAllText(path);
        SaveServes ss = JsonUtility.FromJson<SaveServes>(serves_data);
        foreach (Serve serve in ss.serves)
            add_serve(serve);
        Debug.Log("Loaded " + ss.serves.Length + " serves from " + path);
        return true;
    }
}

[Serializable]
public class SaveServes {
    public Serve[] serves;
    
    public SaveServes(List<Serve> serve_list)
    {
        this.serves = serve_list.ToArray();
    }
}
