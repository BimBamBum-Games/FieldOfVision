using UnityEngine;

public class PlayerController : MonoBehaviour {
    [SerializeField][Range(0, 300f)] float speed = 20f;
    void Update() {
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical);

        movement.Normalize();

        transform.position += speed * Time.deltaTime * movement;
    }
}
