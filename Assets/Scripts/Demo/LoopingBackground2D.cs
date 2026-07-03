using UnityEngine;

namespace Ciga.Demo
{
    public sealed class LoopingBackground2D : MonoBehaviour
    {
        [SerializeField] private float scrollSpeed = 1f;
        [SerializeField] private float tileWidth = 24f;

        private Transform[] tiles;

        private void Awake()
        {
            CacheTiles();
        }

        private void Update()
        {
            if (tiles == null || tiles.Length == 0 || tileWidth <= 0f)
            {
                return;
            }

            float movement = scrollSpeed * Time.deltaTime;
            float recycleX = -tileWidth;
            float rightShift = tileWidth * tiles.Length;

            for (int i = 0; i < tiles.Length; i++)
            {
                Transform tile = tiles[i];
                tile.localPosition += Vector3.left * movement;

                if (tile.localPosition.x <= recycleX)
                {
                    tile.localPosition += Vector3.right * rightShift;
                }
            }
        }

        private void OnValidate()
        {
            tileWidth = Mathf.Max(0.1f, tileWidth);
            scrollSpeed = Mathf.Max(0f, scrollSpeed);
        }

        private void CacheTiles()
        {
            tiles = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                tiles[i] = transform.GetChild(i);
            }
        }
    }
}
