using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public class StartMenuManager : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject optionsMenu;
    public bool startingGame;
    public float multi;
    public float counter;
    public GameObject canvus;
    public int sceneToMove;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void Update()
    {
        if (startingGame == true)
        {
            counter -= multi * Time.deltaTime;
            canvus.GetComponent<CanvasGroup>().alpha = counter;
            if (counter <= 0f)
            {
                Invoke("Change", 1.5f);
            }
        }
    }

    public void OpenOptionsMenu()
    {
        if (!startingGame)
        {
            optionsMenu.SetActive(true);
            mainMenu.SetActive(false);
        }
    }

    public void ReturnToMainMenu()
    {
        if (!startingGame)
        {
            mainMenu.SetActive(true);
            optionsMenu.SetActive(false);
        }
    }

    public void OnStartButton()
    {
        startingGame = true;
    }

    public void Change()
    {
        SceneManager.LoadScene(sceneToMove);
    }
}
