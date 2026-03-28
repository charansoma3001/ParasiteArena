using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [Header("State Names — match exactly what is in your Animator Controller")]
    public string idleState   = "IdleNormal";
    public string walkState   = "Walk";
    public string attackState = "Attack1";
    public string hitState    = "Hit";
    public string deathState  = "Hit";
    public string blockState  = "Block!";

    private Animator _anim;
    private string   _current;

    private void Awake() => _anim = GetComponent<Animator>();

    public void Play(string state)
    {
        if (_anim == null || state == _current) return;
        if (_anim.HasState(0, Animator.StringToHash(state)))
        {
            _anim.Play(state, 0, 0f);
            _current = state;
        }
    }

    public void PlayIdle()   => Play(idleState);
    public void PlayWalk()   => Play(walkState);
    public void PlayAttack() => Play(attackState);
    public void PlayHit()    => Play(hitState);
    public void PlayDeath()  => Play(deathState);
    public void PlayBlock()  => Play(blockState);

    public void TickMovement(bool isMoving)
    {
        if (_current == attackState || _current == hitState ||
            _current == deathState  || _current == blockState) return;
        if (isMoving) PlayWalk();
        else          PlayIdle();
    }
}