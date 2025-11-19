using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    public void LoadTutorial()
    {
        SceneManager.LoadScene("Tutorial");
    }

    public void LoadChapter1()
    {
        SceneManager.LoadScene("Chapter_1");
    }

    public void LoadChapter3()
    {
        SceneManager.LoadScene("Chapter_3");
    }

    public void LoadHome()
    {
        SceneManager.LoadScene("Home");
    }

    public void LoadHomeAgain()
    {
        SceneManager.LoadScene("Home");
    }
}