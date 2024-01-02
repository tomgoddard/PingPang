using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bouncer : MonoBehaviour {

    public AudioClip sound;
    float min_sound_speed = 0.2f;  // Below this speed a rebound makes no sound.
    float max_sound_speed = 15.0f;
    public GameObject marker;
    public bool mark = true;
    public float marker_range = 3f;
    float last_marker_time = 0f;
    float marker_miss_delay = 0.1f;
    public GameObject velocity_arrow, normal_arrow;
    public float perpendicular_rebound = 0.8f;   // Fraction of energy retained in perpendicular bounce.
    public float parallel_rebound = 0f;   // Fraction of energy retained parallel to bounce plane.
    public float friction = 1.0f;   // Maximum ratio of parallel force to perp force during bounce.
    public Vector3 gravity = new Vector3(0f,-9.8f,0f);   // Avoid chattering bounces on horizontal surfaces.
    public bool z_top = false, z_bottom = false, y_top = true, y_bottom = false, x_top = false, x_bottom = false;
    public bool x_top_edge_hits = false, xz_top_edge_hits = false;
    WallState wall_state;        // Position, orientation and velocity of wall.
    WallState last_wall_state;  // Remember last position for moving walls (e.g. paddle).
    public Vector3 ball_strike_velocity;  // Ball velocity at contact.  Used for reporting timing.
    public Vector3 wall_strike_velocity;  // Wall contact point velocity.  Used for reporting speeds.
    public Replay replay;
    public float extra_size = 0f;  // Hit rectangle even if outside bounds by as much as this factor times wall velocity times time step.
    float time_step = 1.0f/90;        // TODO: Use centrally defined time step.

    void Start() {
        wall_state = new WallState (transform);
        last_wall_state = new WallState (transform);
    }

    public Rebound check_for_bounce(BallState bs1, BallState bs2,
                                    float ball_radius, float ball_inertia_coef) {
        // Get previous ball position in previous wall position coordinates.
        Vector3 lp1 = last_wall_state.local_coordinates (bs1.position);
        // Get current ball position in wall coordinates.
        WallState ws = new WallState (transform);
        Vector3 lp2 = ws.local_coordinates (bs2.position);

        // Determine if ball crossed wall.
        Vector3 normal;  // Wall coordinates.
        float offset;  // Wall center to ball center contact distance along normal.
        float frac;
        bool can_tunnel = true;
        bool hit = crossing (lp1, lp2, ball_radius, out normal, out offset, out frac);
        if (!hit) {
            hit = edge_strike(lp1, lp2, ball_radius, out normal, out frac);
            can_tunnel = false;
            if (!hit)
                return null;
        }
        
        // Compute hit position and wall normal in world coordinates.
        Vector3 p = Vector3.Lerp (bs1.position, bs2.position, frac);
        Vector3 n2 = ws.rotation * normal;
        Vector3 n1 = last_wall_state.rotation * normal;
        Vector3 n = Vector3.Slerp (n1, n2, frac);

        // Upate ball velocity and position after bounce.
        Rebound rb = reflect_ball (bs1, bs2, ball_radius, ball_inertia_coef, p, n, frac);

        if (can_tunnel)
            fix_tunneling (rb, n2, offset, ball_radius);

        return rb;
    }

    // Detect if ball crosses wall.  Positions are in wall local coordinates.
    bool crossing(Vector3 position1, Vector3 position2, float radius,
        out Vector3 normal, out float offset, out float frac) {
        Vector3 scale = transform.localScale;
        Vector3 xyz_min = -0.5f * scale, xyz_max = 0.5f * scale;
        float z1 = position1.z, z2 = position2.z, z = xyz_max.z + radius;
        if (z_top && z1 > z && z2 <= z) {
            float f = (z1 - z) / (z1 - z2);
            Vector3 p = position1 + (position2 - position1) * f; // Wall contact position.
            mark_hit_spot(p, xyz_min, xyz_max, 2);
            if (in_bounds_xy(p, xyz_min, xyz_max)) {
                normal = new Vector3 (0, 0, 1); offset = z; frac = f;
                return true;
            }
        }
        z = xyz_min.z - radius;
        if (z_bottom && z1 < z && z2 >= z) {
            float f = (z1 - z) / (z1 - z2);
            Vector3 p = position1 + (position2 - position1) * f; // Wall contact position.
            mark_hit_spot(p, xyz_min, xyz_max, 2);
            if (in_bounds_xy(p, xyz_min, xyz_max)) {
                normal = new Vector3(0,0,-1); offset = -z; frac = f;
                return true;
            }
        }
        float y1 = position1.y, y2 = position2.y, y = xyz_max.y + radius;
        if (y_top && y1 > y && y2 <= y) {
            float f = (y1 - y) / (y1 - y2);
            Vector3 p = position1 + (position2 - position1) * f; // Wall contact position.
            mark_hit_spot(p, xyz_min, xyz_max, 1);
            // TODO: Make marker placement works for y and x faces.
            if (p.x >= xyz_min.x && p.x <= xyz_max.x && p.z >= xyz_min.z && p.z <= xyz_max.z) {
                normal = new Vector3 (0, 1, 0); offset = y; frac = f;
                return true;
            }
        }
        y = xyz_min.y - radius;
        if (y_bottom && y1 < y && y2 >= y) {
            float f = (y1 - y) / (y1 - y2);
            Vector3 p = position1 + (position2 - position1) * f; // Wall contact position.
            mark_hit_spot(p, xyz_min, xyz_max, 1);
            if (p.x >= xyz_min.x && p.x <= xyz_max.x && p.z >= xyz_min.z && p.z <= xyz_max.z) {
                normal = new Vector3(0,-1,0); offset = -y; frac = f;
                return true;
            }
        }
        float x1 = position1.x, x2 = position2.x, x = xyz_max.x + radius;
        if (x_top && x1 > x && x2 <= x) {
            float f = (x1 - x) / (x1 - x2);
            Vector3 p = position1 + (position2 - position1) * f; // Wall contact position.
            mark_hit_spot(p, xyz_min, xyz_max, 0);
            if (p.y >= xyz_min.y && p.y <= xyz_max.y && p.z >= xyz_min.z && p.z <= xyz_max.z) {
                normal = new Vector3(1,0,0); offset = x; frac = f;
                return true;
            }
        }
        x = xyz_min.x - radius;
        if (x_bottom && x1 < x && x2 >= x) {
            float f = (x1 - x) / (x1 - x2);
            Vector3 p = position1 + (position2 - position1) * f; // Wall contact position.
            mark_hit_spot(p, xyz_min, xyz_max, 0);
            if (p.y >= xyz_min.y && p.y <= xyz_max.y && p.z >= xyz_min.z && p.z <= xyz_max.z) {
                normal = new Vector3(-1,0,0); offset = -x; frac = f;
                return true;
            }
        }
        normal = Vector3.zero;
        offset = 0;
        frac = 0;
        return false;
    }

    bool in_bounds_xy(Vector3 p, Vector3 xyz_min, Vector3 xyz_max)
    {
        float es = extra_size;
        if (es == 0)
            return (p.x >= xyz_min.x && p.x <= xyz_max.x && p.y >= xyz_min.y && p.y <= xyz_max.y);
        
        Vector3 vlocal = Quaternion.Inverse(wall_state.rotation) * wall_state.velocity;
        float xpad = Mathf.Abs(es * vlocal.x * time_step);
        float ypad = Mathf.Abs(es * vlocal.y * time_step);
        return (p.x >= xyz_min.x - xpad && p.x <= xyz_max.x + xpad
                && p.y >= xyz_min.y - ypad && p.y <= xyz_max.y + ypad);
    }

    bool edge_strike(Vector3 b1, Vector3 b2, float radius,
                    out Vector3 normal, out float frac) {
        if (x_top_edge_hits) {
            // x-edge at positive y and z = 0.
            // This is what is needed for net edge strike.
            Vector3 scale = transform.localScale;
            float x = 0.5f*scale.x, y = 0.5f*scale.y;
            Vector3 e1 = new Vector3(-x, y, 0f);
            Vector3 e2 = new Vector3(x, y, 0f);
            bool hit = line_segment_strike(b1, b2, e1, e2, radius, out normal, out frac);
            return hit;
        } else if (xz_top_edge_hits) {
            // x or z axis edges at positive y.
            // This is what is needed for table edge strike.
            Vector3 scale = transform.localScale;
            float x = 0.5f*scale.x, y = 0.5f*scale.y, z = 0.5f*scale.z;
            bool hit = (line_segment_strike(b1, b2, new Vector3(-x,y,z), new Vector3(x,y,z), radius, out normal, out frac) ||
                line_segment_strike(b1, b2, new Vector3(-x,y,-z), new Vector3(x,y,-z), radius, out normal, out frac) ||
                line_segment_strike(b1, b2, new Vector3(x,y,-z), new Vector3(x,y,z), radius, out normal, out frac) ||
                line_segment_strike(b1, b2, new Vector3(-x,y,-z), new Vector3(-x,y,z), radius, out normal, out frac));
            return hit;
        }
        normal = Vector3.zero;
        frac = 0;
        return false;
    }

    bool line_segment_strike(Vector3 b1, Vector3 b2, Vector3 e1, Vector3 e2, float radius,
                             out Vector3 normal, out float frac) {
        normal = Vector3.zero;
        frac = 0f;

        Vector3 b12 = b2 - b1;
        Vector3 b1e1 = b1 - e1;
        Vector3 e12 = e2 - e1;
        Vector3 e = e12.normalized;
        Vector3 u = b1e1 - Vector3.Dot(b1e1,e)*e;
        Vector3 v = b12 - Vector3.Dot(b12,e)*e;
        float A = v.sqrMagnitude, B = 2f*Vector3.Dot(u,v), C = u.sqrMagnitude - radius*radius;
        if (A == 0)
            return false;    // Ball line is parallel edge line.
        float D = B*B - 4f*A*C;
        if (D <= 0)
            return false;   // Ball line and edge line too far apart
        float t = (-B - Mathf.Sqrt(D)) / (2f * A);
        if (t < 0 || t > 1)
            return false;    // Contact point is beyond ends of ball segement.
        Vector3 bc = b1 + t*b12;
        float s = Vector3.Dot(bc - e1, e) / e12.magnitude;
        if (s < 0 || s > 1)
            return false;    // Contact point is beyond ends of edge segment.
        Vector3 ec = e1 + s*e12;
        normal = (bc - ec).normalized;
        frac = t;
        return true;
    }

    void mark_hit_spot(Vector3 p, Vector3 xyz_min, Vector3 xyz_max, int axis) {
        
        float t = Time.time;
        if (!mark || marker == null || t - last_marker_time <= marker_miss_delay)
            return;

        Vector3 miss = p - 0.5f * (xyz_min + xyz_max);
        Vector3 size = xyz_max - xyz_min;
        for (int a = 0 ; a < 3 ; ++a)
            if (a != axis && Mathf.Abs(miss[a]) > 0.5f * marker_range * size[a])
                return;  // Hit outside bounds.
        float z = (p.z > xyz_max.z ? xyz_max.z : xyz_min.z);
        Vector3 missp = new Vector3(p.x, p.y, p.z);
        missp[axis] = (p[axis] > xyz_max[axis] ? xyz_max[axis] : xyz_min[axis]);
        missp += transform.localPosition;
        marker.transform.position = transform.parent.TransformPoint (missp);
        last_marker_time = t;

        if (replay != null)
            replay.record_positions ();
    }

    void show_motion_arrows(Vector3 position, Vector3 normal, Vector3 velocity) {

        if (velocity_arrow != null) {
            Transform t = velocity_arrow.transform;
            t.position = position + .05f * velocity;
            t.rotation = Quaternion.FromToRotation(new Vector3(0,1,0), velocity.normalized);
            t.localScale = new Vector3(.01f, .05f * velocity.magnitude, .01f);
        }

        if (normal_arrow != null) {
            Transform t = normal_arrow.transform;
            t.position = position + .05f * normal;
            t.rotation = Quaternion.FromToRotation(new Vector3(0,1,0), normal);
            //t.localScale = new Vector3(.01f, .05f, .01f);
        }

    }

    Rebound reflect_ball(BallState bs1, BallState bs2,
                 float ball_radius, float ball_inertia_coef,
                 Vector3 position, Vector3 normal, float frac) {
        // Position and normal in world coordinates.

        float time_step = bs2.time - bs1.time;

        // Ball velocity in world coordiantes.
        Vector3 bv0 = Vector3.Lerp(bs1.velocity, bs2.velocity, frac);
        ball_strike_velocity = bv0;

        // Wall velocity at contact point in world coordinates.
        Vector3 wv = Vector3.Lerp (last_wall_state.velocity, wall_state.velocity, frac);
        // Correct wall contact point velocity using wall angular velocity.
        Vector3 wav = Vector3.Slerp (last_wall_state.angular_velocity, wall_state.angular_velocity, frac);
        Vector3 wall_center = Vector3.Lerp (last_wall_state.position, wall_state.position, frac);
        Vector3 wvt = Vector3.Cross (wav, position - wall_center);
        wv += wvt;
        wall_strike_velocity = wv;

        // Ball velocity in wall rest frame.
        Vector3 bv = bv0 - wv;
        Vector3 av0 = Vector3.Lerp(bs1.angular_velocity, bs2.angular_velocity, frac);
        Vector3 av = av0;

        float bn = Vector3.Dot (bv, normal);
//	if (gameObject.name.StartsWith ("rubber"))
//	    Debug.Log("Ball contact speed " + bn);
        if (bn > 0) {
            // Paddle caught up to ball even though it is going slower than ball.
            // Happens because the paddle velocity is not consistent with the two paddle positions.
            // This is because I use the reported hand velocity.  I could instead derive paddle
            // velocity from positions which would be more consistent, but I don't have the times
            // for the paddle positions, they are not available from the Unity Oculus 1.18.1 API.
            // So just pretend the paddle did not hit the ball, its velocity stays the same.
            if (gameObject.name.StartsWith ("rubber")) {
                Debug.Log ("Ball hit while receding from paddle with speed " + bn);
		Debug.Log ("Ball velocity " + bv0 + " wall velocity " + wv + " normal " + normal);
	    }
        } else {
            Vector3 vperp = bn * normal;
            Vector3 vpar = bv - vperp;  // Velocity parallel wall.
            Vector3 vrot = Vector3.Cross (bs2.angular_velocity, -ball_radius * normal);
            float fpar = 1 + Mathf.Sqrt (parallel_rebound);
            Vector3 vparimp = (vpar + vrot) * (-fpar / (1.0f + 1.0f / ball_inertia_coef));
            float fperp = 1 + Mathf.Sqrt (perpendicular_rebound);
            Vector3 vperpimp = -vperp * fperp;
            float vparmax = friction * (vperpimp.magnitude - time_step * Vector3.Dot(gravity, normal));
            if (vparimp.magnitude > vparmax) {
                // Ball skids because friction is not enough.
                // Reduce parallel impulse to friction limit.
                float f = vparmax / vparimp.magnitude;
                vparimp *= f;
                if (gameObject.name.StartsWith ("rubber"))
                    Debug.Log ("skidded " + f);
            }
            //Debug.Log ("vparimp " + vparimp.ToString ("F4") + " vrot " + vrot.ToString("F4") + " vpar " + vpar.ToString("F4"));
            // Reflect ball in wall rest frame.
            bv += vperpimp + vparimp;
            // Adjust ball spin.
            av -= Vector3.Cross (normal, vparimp) / (ball_inertia_coef * ball_radius);
            //Vector3 vpar2 = vpar + vparimp;
            //Vector3 vrot2 = Vector3.Cross (av, -ball.radius * normal);
            //Debug.Log ("fpar " + fpar + " res " + (fpar-1) * (vpar + vrot).magnitude + " to " + (vpar2 + vrot2).magnitude);
        }

        //Debug.Log ("Bounce " + bv.ToString("F3") + " normal " + normal);

        // Shift from wall rest frame to world frame.
        Vector3 rv = bv + wv;
        /*
	if (gameObject.name.StartsWith ("rubber"))
	    Debug.Log("Ball speed away from wall " + bv.magnitude + ", wall speed " + wv.magnitude + ", ball speed " + rv.magnitude);
        if (gameObject.name.StartsWith ("rubber")) {
            Debug.Log ("Hit " + gameObject.name + " ball initial velocity " + ball.velocity +
                " final velocity " + rv + " paddle velocity " + velocity + " paddle speed " + velocity.magnitude +
            " initial angular velocity " + ball.angular_velocity + " final " + av);
            // Debug.Log ("position " + ball.transform.position.ToString ("F4"));
            //Vector3 cv = bv - ball.radius * Vector3.Cross (av, normal); // Contact point velocity.
            //Vector3 cvpar = cv - Vector3.Dot(cv, normal) * normal;
            // Debug.Log ("Contact vel " + cvpar.ToString ("F4") + " normal " + normal + " nmag " + normal.magnitude);
        }
        */

        // Check wall motion back calculation.
        /*
        Vector3 pv, pn;
        wall_motion (bv0, rv, ball.angular_velocity, av, ball.radius, ball.inertia_coef, out pv, out pn);
        Debug.Log ("Back compute v = " + velocity.ToString ("F3") + " predict " + pv.ToString ("F3") +
        " n = " + normal.ToString ("F3") + " predict " + pn.ToString ("F3"));
        */

        // Set new ball velocity, angular velocity and position.
        Vector3 rp = position;
        float t = (1 - frac) * time_step;

        // Gravity corrected rebound.
        float gn = -Vector3.Dot(gravity, normal);
        float vn = Vector3.Dot (rv, normal);
        // For small velocity would get multiple bounces in one time step.
        // In that case adjust normal velocity to zero.
        float tg = (gn * t > vn ? vn/gn : t);
        rv += tg * gravity;
        // Adjust position by full post-rebound time so rolling can occur.
        rp += t * rv;
        //rp -= (0.5f * t * t * gn) * normal;

        float t0 = bs1.time + frac * time_step;
        //Debug.Log ("Bounce " + gameObject.name + " frac " + frac);
        Rebound rb = new Rebound (bs1.ball, t0, position, bv0, av0,
                                  bs2.time, rp, rv, av, this, -bn);
        /*
        if (name == "rubber red")
            Debug.Log ("ball pre hit vel " + bv0.ToString("F4") + " post hit vel " + rv.ToString ("F4") +
                " pre hit spin " + av0.ToString("F4") + " post hit spin " + av.ToString("F4"));
        */

        show_motion_arrows(wall_center, normal, wv);

        return rb;
    }

    void fix_tunneling(Rebound rb, Vector3 normal, float offset, float ball_radius) {
        Vector3 rp = rb.final.position;
        float nd = Vector3.Dot (rp - wall_state.position, normal) - offset;
        float tolerance = 0.01f * ball_radius;
        if (nd < tolerance) {
            // Ball fell through wall. Happens at low speeds when
            // paddle motion does not exactly match paddle velocities.
            // Put ball back in front of wall.
            rp += (-nd + tolerance) * normal;
            rb.final.position = rp;
        }
    }

    public void play_bounce_sound(Ball ball, float vperp) {
        if (vperp < min_sound_speed)
            return;
        // Play bounce sound.
        AudioSource audio = ball.GetComponent<AudioSource> ();
        audio.clip = sound;
        audio.volume = Mathf.Clamp (vperp, min_sound_speed, max_sound_speed) / max_sound_speed;
        audio.Play ();
    }

    public void move_wall(Vector3 velocity, Vector3 angular_velocity) {
        last_wall_state.set(wall_state);
        wall_state.set(transform, velocity, angular_velocity);
    }

    public void jump_wall() {
        last_wall_state.set(wall_state);
    }

    //
    // Find paddle velocity and orientation in order to bounce ball from
    // a specified incoming velocity and spin, to a specified outgoing
    // velocity and spin.
    //
    // The change in ball spin must be perpendicular to the change in ball velocity.
    // Also the magnitude of the change in spin cannot exceed 1.5 times the magnitude
    // of the change in velocity.
    //
    // This routine assumes the friction coefficient is infinite so the ball cannot
    // slip on the paddle.
    //
    public void wall_motion(Vector3 v_in, Vector3 v_out, Vector3 w_in, Vector3 w_out,
                            float ball_radius, float ball_inertia_coef,
                            float grazing_slope_limit, out Vector3 v, out Vector3 n) {
        Vector3 dv = v_out - v_in;
        Vector3 dw = w_out - w_in;

        // Change w_out to make dw perpedicular to dv.
        Vector3 dwp = Vector3.ProjectOnPlane (dw, dv);
        Vector3 wp_out = w_in + dwp;

        float r = ball_radius;
        float A = ball_inertia_coef * r * dwp.magnitude / dv.magnitude;
        float f = grazing_slope_limit;
        float max_A = f / Mathf.Sqrt (1 + f * f);
        if (A > max_A) {
            // Change w_out so maximum spin transfer is not exceeded.
            dwp *= max_A / A;
            wp_out = w_in + dwp;
            A = max_A;
        }
        float D = Mathf.Sqrt (1 - A * A);
        n = A * Vector3.Cross (dwp, dv).normalized + D * dv.normalized;

        float fperp = Mathf.Sqrt (perpendicular_rebound);
        float fpar = Mathf.Sqrt (parallel_rebound);
        Vector3 vperp = (v_out + fperp * v_in) / (1 + fperp);
        //Debug.Log ("vperp " + vperp.ToString ("F4") + " v_out " + v_out.ToString("F4") + " v_in " + v_in.ToString("F4")
        //    + " fperp " + fperp.ToString("F4") + " vo/vi " + (v_out.y/v_in.y).ToString("F4"));
        Vector3 vpar = ((v_out + r * Vector3.Cross (n, wp_out)) + fpar * (v_in + r * Vector3.Cross (n, w_in))) / (1 + fpar);
        v = Vector3.Project (vperp, n) + Vector3.ProjectOnPlane (vpar, n);
    }
}

public class WallState {
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angular_velocity;

    public WallState(Transform t) {
        position = t.position;
        rotation = t.rotation;
        velocity = Vector3.zero;
        angular_velocity = Vector3.zero;
    }

    public void set(Transform t, Vector3 velocity, Vector3 angular_velocity) {
        position = t.position;
        rotation = t.rotation;
        this.velocity = velocity;
        this.angular_velocity = angular_velocity;
    }

    public void set(WallState s) {
        position = s.position;
        rotation = s.rotation;
        velocity = s.velocity; 
        angular_velocity = s.angular_velocity;
    }

    public Vector3 local_coordinates(Vector3 p) {
        return Quaternion.Inverse(rotation) * (p - position);
    }
}

public class Rebound {
    public BallState contact, final;
    public float vperp;  // Relative velocity at contact.  Used for setting volume of impact sound.
    public Bouncer bouncer;

    public Rebound(Ball ball, float ct, Vector3 cpos, Vector3 cvel, Vector3 cavel,
                    float ft, Vector3 fpos, Vector3 fvel, Vector3 favel,
                    Bouncer bn, float vperp) {
        contact = new BallState (ball, ct, cpos, cvel, cavel);
        final = new BallState (ball, ft, fpos, fvel, favel);
        bouncer = bn;
        this.vperp = vperp;
    }

    public void set_ball(Ball ball, bool play_sound = true) {
        ball.set_motion (final);

        if (play_sound)
            bouncer.play_bounce_sound (ball, vperp);
    }
}
