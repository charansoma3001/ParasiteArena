using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    Animator anim;
    Rigidbody2D rb;

    public float speed = 2f;
    private int maxHealth = 100;
    private int currentHealth;
    bool isDead = false;
    float moveHorizontal, moveVertical;

    Vector2 movement;
    int facingDirection = 1; // -1 for left, 1 for right

    private void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (isDead)
        {
            movement = Vector2.zero;
            anim.SetFloat("Speed", 0);
            return;
        }

        moveHorizontal = Input.GetAxisRaw("Horizontal");
        moveVertical = Input.GetAxisRaw("Vertical");

        movement = new Vector2(moveHorizontal, moveVertical).normalized;
        anim.SetFloat("Speed", movement.sqrMagnitude);

        if (movement.x != 0)
            facingDirection = movement.x > 0 ? -1 : 1;

        transform.localScale = new Vector2(facingDirection, 1);
    }

    private void FixedUpdate()
    {
        rb.velocity = movement * speed;
    }
}