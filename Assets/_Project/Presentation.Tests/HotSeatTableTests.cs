using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Presentation.Table;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LiarsBar.Presentation.Tests
{
    public class HotSeatTableTests
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator ThreeHumans_KeepHandsPrivateUntilGameOver()
        {
            yield return new EnterPlayMode();
            var scene = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Bootstrap/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return scene;
            Click("Войти в бар");
            if (GameObject.Find("Rules") != null)
            {
                const string key = "LiarsBar.RulesSeen";
                bool existed = PlayerPrefs.HasKey(key);
                int previous = PlayerPrefs.GetInt(key);
                Click("В меню");
                if (existed) PlayerPrefs.SetInt(key, previous);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Click("На одном устройстве");
            Click("Добавить игрока");
            var names = new[] { "Лена", "Иван", "Анна" };
            var fields = GameObject.Find("Participants").GetComponentsInChildren<TMP_InputField>();
            Assert.AreEqual(names.Length, fields.Length);
            for (int i = 0; i < fields.Length; i++) fields[i].text = names[i];
            Click("Начать игру");

            var table = Object.FindAnyObjectByType<TableView>().transform;
            var hand = table.Find("Hand");
            var pass = table.Find("PassDeviceView");
            var reveal = table.Find("Reveal");
            var gameOver = table.Find("GameOver");
            for (int match = 0; match < 3; match++)
            {
                if (match > 0)
                {
                    Press(gameOver.Find("Box/Button"));
                    Assert.IsFalse(gameOver.gameObject.activeSelf);
                    Assert.IsFalse(reveal.gameObject.activeSelf);
                }
                var seen = new HashSet<string>();
                int checks = 0;
                for (int turn = 0; turn < 300 && !gameOver.gameObject.activeSelf; turn++)
                {
                    Assert.IsEmpty(hand.GetComponentsInChildren<CardView>());
                    while (table.GetComponent<TableView>().IsPlayingEvent && !reveal.gameObject.activeSelf)
                    {
                        Assert.IsEmpty(hand.GetComponentsInChildren<CardView>());
                        yield return null;
                    }
                    if (gameOver.gameObject.activeSelf) break;
                    Assert.IsEmpty(hand.GetComponentsInChildren<CardView>());
                    if (reveal.gameObject.activeSelf)
                    {
                        Assert.IsFalse(pass.gameObject.activeSelf);
                        Press(reveal.Find("Box/Button"));
                        checks++;
                        continue;
                    }
                    Assert.IsTrue(pass.gameObject.activeSelf);
                    string title = pass.Find("Recipient").GetComponent<TMP_Text>().text;
                    string recipient = names.Single(n => title == $"Передайте устройство: {n}");
                    seen.Add(recipient);
                    Assert.IsFalse(table.Find("Play").gameObject.activeSelf);
                    Assert.IsFalse(table.Find("Liar").gameObject.activeSelf);
                    Press(pass.Find("Confirm"));
                    Assert.IsFalse(pass.gameObject.activeSelf);
                    var cards = hand.GetComponentsInChildren<CardView>();
                    var liar = table.Find("Liar").GetComponent<Button>();
                    if (liar.interactable) Press(liar.transform);
                    else
                    {
                        Assert.IsNotEmpty(cards);
                        Press(cards[0].transform);
                        Press(table.Find("Play"));
                    }
                    Assert.IsEmpty(hand.GetComponentsInChildren<CardView>());
                }
                Assert.IsTrue(gameOver.gameObject.activeSelf);
                Assert.IsFalse(pass.gameObject.activeSelf);
                Assert.Greater(checks, 0);
                CollectionAssert.AreEquivalent(names, seen);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (UnityEditor.EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        private static void Click(string name) => Press(GameObject.Find(name).transform);

        private static void Press(Transform control)
        {
            var button = control.GetComponent<Button>();
            Assert.IsTrue(button.isActiveAndEnabled && button.IsInteractable(), control.name);
            button.onClick.Invoke();
        }
    }
}
