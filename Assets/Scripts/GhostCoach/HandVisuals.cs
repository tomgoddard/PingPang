using System.Collections.Generic;
using UnityEngine;

// Toggle to hide the virtual racket and the ball in the hand, e.g. to see
// only your own arm and the coach in mixed reality.  Only how they look:
// the racket still hits balls, and a ball you toss shows as soon as it
// leaves the hand, so you can see where it goes.
public class HandVisuals : MonoBehaviour {

    public Play play;
    public bool hidden { get; private set; }

    Dictionary<Renderer, bool> paddle_renderers = new Dictionary<Renderer, bool>();
    Ball hidden_ball;

    public void apply_hidden(bool hide) {
        hidden = hide;
        if (hide)
            hide_paddle();
        else
            show_paddle();
        update_ball();
    }

    void hide_paddle() {
        show_paddle();      // Forget an earlier hide so states are not mixed up.
        foreach (MeshRenderer r in play.paddle_hand.held_paddle.GetComponentsInChildren<MeshRenderer>(true)) {
            paddle_renderers[r] = r.enabled;
            r.enabled = false;
            GhostVisuals.hidden_by_toggle.Add(r);
        }
    }

    void show_paddle() {
        foreach (KeyValuePair<Renderer, bool> item in paddle_renderers) {
            item.Key.enabled = item.Value;
            GhostVisuals.hidden_by_toggle.Remove(item.Key);
        }
        paddle_renderers.Clear();
    }

    // The ball in the hand: the free hand's, or the paddle hand's.  A ball
    // that was hidden but has left the hand is shown again.
    void update_ball() {
        Ball held = play.free_hand.held_ball;
        if (held == null)
            held = play.paddle_hand.held_ball;
        if (hidden_ball != null && (hidden_ball != held || !hidden)) {
            hidden_ball.GetComponent<MeshRenderer>().enabled = true;
            hidden_ball = null;
        }
        if (hidden && held != null) {
            held.GetComponent<MeshRenderer>().enabled = false;
            hidden_ball = held;
        }
    }

    // After Play.Update() has moved balls and hands.
    void LateUpdate() {
        update_ball();
    }
}
