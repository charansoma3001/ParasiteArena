using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [Header("State Names")]
    public string idleState = "IdleNormal";
    public string walkState = "Walk";
    public string attackState = "Attack1";
    public string hitState = "Hit";
    public string deathState = "Hit";
    private Animator _anim;
    private SpriteRenderer _spriteRenderer;
    private string _current;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Play(string state)
    {
        if (_anim == null || state == _current) return;
        if (_anim.HasState(0, Animator.StringToHash(state)))
        {
            _anim.Play(state, 0, 0f);
            _current = state;
        }
    }

    public void PlayIdle() => Play(idleState);
    public void PlayWalk() => Play(walkState);
    public void PlayAttack() => Play(attackState);
    public void PlayHit() => Play(hitState);
    public void PlayDeath() => Play(deathState);

    public void TickMovement(bool isMoving)
    {
        if (_current == attackState || _current == hitState) return;
        if (isMoving) PlayWalk();
        else PlayIdle();
    }

    public void SetFacing(Vector2 dir)
    {
        if (_spriteRenderer == null) return;
        if (dir.x > 0.1f)  _spriteRenderer.flipX = false;
        else if (dir.x < -0.1f) _spriteRenderer.flipX = true;
    }
}