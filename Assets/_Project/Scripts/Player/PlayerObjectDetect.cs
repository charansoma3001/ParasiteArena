using System.Collections.Generic;
using UnityEngine;

public class PlayerObjectDetect : MonoBehaviour
{
    public float detectRadius = 2.5f;
    public LayerMask objectLayer;

    private readonly List<ObjectFade> fadedNow = new List<ObjectFade>();
    private readonly List<ObjectFade> fadedLastFrame = new List<ObjectFade>();

    private void Update()
    {
        fadedLastFrame.Clear();
        fadedLastFrame.AddRange(fadedNow);
        fadedNow.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRadius, objectLayer);

        foreach (Collider2D hit in hits)
        {
            ObjectFade fader = hit.GetComponentInParent<ObjectFade>();
            if (fader != null)
            {
                fader.FadeOut();

                if (!fadedNow.Contains(fader))
                    fadedNow.Add(fader);
            }
        }

        foreach (ObjectFade oldFader in fadedLastFrame)
        {
            if (!fadedNow.Contains(oldFader) && oldFader != null)
            {
                oldFader.FadeIn();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}