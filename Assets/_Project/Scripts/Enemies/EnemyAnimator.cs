using UnityEngine;

/// Sits on the same child GameObject as the Animator.
/// Drives animation with direct Play() calls — no wired transitions needed at all.
/// Set every state's "Can Transition To Self" = true in the Animator Controller,
/// and just leave states disconnected. This script jumps to them directly.
///
/// Override the name fields in the Inspector to match your exact state names:
///   Swordsman: IdleNormal, Walk!, Attack1, Hit, Block!
///   Archer:    IdleNormal, Walk,  Atk!,    hit
public class EnemyAnimator : MonoBehaviour
{
    [Header("State Names — must match exactly what is in your Animator Controller")]
    public string idleState   = "IdleNormal";
    public string walkState   = "Walk";
    public string attackState = "Attack1";
    public string hitState    = "Hit";
    public string deathState  = "Hit";
    public string blockState  = "Block!";

    private Animator _anim;
    private string   _current;

    private void Awake() => _anim = GetComponent<Animator>();

    public void Play(string stateName)
    {
        if (_anim == null || stateName == _current) return;
        if (_anim.HasState(0, Animator.StringToHash(stateName)))
        {
            _anim.Play(stateName, 0, 0f);
            _current = stateName;
        }
    }

    public void PlayIdle()   => Play(idleState);
    public void PlayWalk()   => Play(walkState);
    public void PlayAttack() => Play(attackState);
    public void PlayHit()    => Play(hitState);
    public void PlayDeath()  => Play(deathState);
    public void PlayBlock()  => Play(blockState);

    /// Call this every frame from EnemyAI to reset to idle when stopped
    public void TickMovement(bool isMoving)
    {
        if (_current == attackState || _current == hitState ||
            _current == deathState  || _current == blockState) return;
        if (isMoving) PlayWalk();
        else          PlayIdle();
    }
}
