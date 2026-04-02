using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [Header("State Names")]
    public string idleState = "IdleNormal";
    public string walkState = "Walk";
    public string attackState = "Attack1";
    public string hitState = "Hit";
    public string deathState = "Hit";
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private string current;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Play(string state)
    {
        if (anim == null || state == current) return;
        if (anim.HasState(0, Animator.StringToHash(state)))
        {
            anim.Play(state, 0, 0f);
            current = state;
        }
    }

    public void PlayIdle() => Play(idleState);
    public void PlayWalk() => Play(walkState);
    public void PlayAttack() => Play(attackState);
    public void PlayHit() => Play(hitState);
    public void PlayDeath() => Play(deathState);

    public void TickMovement(bool isMoving)
    {
        if (current == attackState || current == hitState) return;
        if (isMoving) PlayWalk();
        else PlayIdle();
    }

    public void SetFacing(Vector2 dir)
    {
        if (spriteRenderer == null) return;
        if (dir.x > 0.1f)  spriteRenderer.flipX = false;
        else if (dir.x < -0.1f) spriteRenderer.flipX = true;
    }
}