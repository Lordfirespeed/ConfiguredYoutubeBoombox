using UnityEngine;

namespace ConfiguredYoutubeBoombox
{
    internal class YoutubeBoomboxGUI : MonoBehaviour
    {
        private float menuHeight;
        private float menuWidth;
        private float menuX;
        private float menuY;

        private string url = "Youtube URL";

        private void Awake()
        {
            menuWidth = Screen.width / 3;
            menuHeight = Screen.width / 4;
            menuX = Screen.width / 2 - menuWidth / 2;
            menuY = Screen.height / 2 - menuHeight / 2;
        }

        public void OnGUI()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.Confined;
            GUI.Box(new Rect(menuX, menuY, menuWidth, menuHeight), "Youtube Boombox");
            url = GUI.TextField(new Rect(menuX + 25, menuY + 20, menuWidth - 50, 50), url);

            if (GUI.Button(new Rect(menuX + 25, menuY + 50 + 50, menuWidth - 50, 50), "Play"))
            {
                if (gameObject.TryGetComponent(out BoomboxController controller))
                {
                    controller.DestroyGUI();
                    controller.PlaySong(url);
                }

                Cursor.visible = false;
                //Cursor.lockState = CursorLockMode.Locked;

                Destroy(this);
            }

            if (GUI.Button(new Rect(menuX + 25, menuY + 50 + 50 + 50, menuWidth - 50, 50), "Close"))
            {
                Cursor.visible = false;
                //Cursor.lockState = CursorLockMode.Locked;

                if (gameObject.TryGetComponent(out BoomboxController controller)) controller.DestroyGUI();

                Destroy(this);
            }
        }
    }
}