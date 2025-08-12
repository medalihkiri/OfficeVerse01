using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D.Animation;

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance;

    public List<SpriteLibraryAsset> CharacterSpriteLibraries;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        LoadCharacterAnimations();
    }

    public void LoadCharacterAnimations()
    {
        CharacterSpriteLibraries = Resources.LoadAll<SpriteLibraryAsset>("SpriteLibrary").ToList();
    }
}
