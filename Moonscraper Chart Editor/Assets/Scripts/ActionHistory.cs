﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class ActionHistory
{
    const float ACTION_WINDOW_TIME = 0.2f;
    int historyPoint;
    List<Action[]> actionList;
    List<float> timestamps;

    public bool canUndo { get { return historyPoint >= 0; } }
    public bool canRedo { get { return historyPoint + 1 < actionList.Count; } }

    public ActionHistory()
    {
        actionList = new List<Action[]>();
        timestamps = new List<float>();
        historyPoint = -1;
    }

    public void Insert(Action[] action)
    {
        // Clear all actions above the history point
        actionList.RemoveRange(historyPoint + 1, actionList.Count - (historyPoint + 1));
        timestamps.RemoveRange(historyPoint + 1, timestamps.Count - (historyPoint + 1));

        // Add the action in
        actionList.Add(action);
        timestamps.Add(Time.time);

        ++historyPoint;
    }

    public void Insert(Action action)
    {
        Insert(new Action[] { action });
    }

    public bool Undo(ChartEditor editor)
    {
        if (canUndo)
        {
            ChartEditor.editOccurred = true;
            float frame = timestamps[historyPoint];

            int actionsUndone = 0;
            SongObject setPos = null;
            while (historyPoint >= 0 && Mathf.Abs(timestamps[historyPoint] - frame) < ACTION_WINDOW_TIME)
            {
                for (int i = actionList[historyPoint].Length - 1; i >= 0; --i)
                {
                    setPos = actionList[historyPoint][i].Revoke(editor);
                    ++actionsUndone;
                }

                --historyPoint;
            }

            editor.currentChart.updateArrays();
            editor.currentSong.updateArrays();
            if (Toolpane.currentTool != Toolpane.Tools.Note)
                editor.currentSelectedObject = null;

            if (setPos.position < editor.currentSong.WorldYPositionToChartPosition(editor.visibleStrikeline.position.y) || setPos.position > editor.maxPos)
                editor.movement.SetPosition(setPos.position);

            if (setPos.GetType().IsSubclassOf(typeof(ChartObject)))
            {
                if (Globals.viewMode == Globals.ViewMode.Song)          // Placing local object while in chart view
                    editor.globals.ToggleSongViewMode(false);
            }
            else if (Globals.viewMode == Globals.ViewMode.Chart)        // Placing global object while in local view
                editor.globals.ToggleSongViewMode(true);

            Debug.Log("Undo: " + actionsUndone + " actions");
            return true;
        }

        Debug.Log("Undo: 0 actions");

        return false;
    }

    public bool Redo(ChartEditor editor)
    {
        if (canRedo)
        {
            ChartEditor.editOccurred = true;
            float frame = timestamps[historyPoint + 1];
            int actionsUndone = 0;
            SongObject setPos = null;

            while (historyPoint + 1 < actionList.Count && Mathf.Abs(timestamps[historyPoint + 1] - frame) < ACTION_WINDOW_TIME)
            {
                ++historyPoint;
                for (int i = 0; i < actionList[historyPoint].Length; ++i)
                {
                    setPos = actionList[historyPoint][i].Invoke(editor);
                    ++actionsUndone;
                }
            }

            editor.currentChart.updateArrays();
            editor.currentSong.updateArrays();
            if (Toolpane.currentTool != Toolpane.Tools.Note)
                editor.currentSelectedObject = null;

            if (setPos.position < editor.currentSong.WorldYPositionToChartPosition(editor.visibleStrikeline.position.y) || setPos.position > editor.maxPos)
                editor.movement.SetPosition(setPos.position);

            Debug.Log("Redo: " + actionsUndone + " actions");
            return true;
        }

        Debug.Log("Redo: 0 actions");

        return false;
    }

    public abstract class Action
    {
        protected SongObject[] songObjects;

        protected Action(SongObject[] _songObjects)
        {
            songObjects = new SongObject[_songObjects.Length];

            for (int i = 0; i < _songObjects.Length; ++i)
            {
                songObjects[i] = _songObjects[i].Clone();
            }
        }

        public abstract SongObject Revoke(ChartEditor editor);
        public abstract SongObject Invoke(ChartEditor editor);
    }

    public class Add : Action
    {
        public Add(SongObject[] songObjects) : base(songObjects) { }
        public Add(SongObject songObjects) : base(new SongObject[] { songObjects }) { }

        public override SongObject Invoke(ChartEditor editor)
        {
            foreach (SongObject songObject in songObjects)
            {
                PlaceSongObject.AddObjectToCurrentEditor(songObject.Clone(), editor, false);
            }

            return songObjects[0];
        }

        public override SongObject Revoke(ChartEditor editor)
        {
            return new Delete(songObjects).Invoke(editor);
        }
    }

    public class Delete : Action
    {
        public Delete(SongObject[] songObjects) : base(songObjects) { }
        public Delete(SongObject songObjects) : base(new SongObject[] { songObjects }) { }

        public override SongObject Invoke(ChartEditor editor)
        {
            foreach (SongObject songObject in songObjects)
            {
                SongObject foundSongObject;
                SongObject[] arrayToSearch;
                int arrayPos;

                // Find each item
                if (songObject.GetType().IsSubclassOf(typeof(ChartObject)) || songObject.GetType() == typeof(ChartObject))
                {
                    arrayToSearch = editor.currentChart.chartObjects;
                }
                else
                {
                    if (songObject.GetType().IsSubclassOf(typeof(Event)) || songObject.GetType() == typeof(Event))
                        arrayToSearch = editor.currentSong.events;

                    else
                        arrayToSearch = editor.currentSong.syncTrack;
                }

                arrayPos = SongObject.FindObjectPosition(songObject, arrayToSearch);

                if (arrayPos == SongObject.NOTFOUND)
                    continue;
                else
                    foundSongObject = arrayToSearch[arrayPos];

                foundSongObject.Delete(false);
            }

            return songObjects[0];
        }

        public override SongObject Revoke(ChartEditor editor)
        {
            return new Add(songObjects).Invoke(editor);
        }
    }

    public class Modify : Action
    {
        public SongObject before { get { return songObjects[0]; } set { songObjects[0] = value.Clone(); } }
        public SongObject after { get { return songObjects[1]; } set { songObjects[1] = value.Clone(); } }

        public Modify(SongObject before, SongObject after) : base(new SongObject[] { before, after }) { }

        public override SongObject Invoke(ChartEditor editor)
        {
            new Delete(before).Invoke(editor);
            return new Add(after).Invoke(editor);
        }

        public override SongObject Revoke(ChartEditor editor)
        {
            new Delete(after).Invoke(editor);
            return new Add(before).Invoke(editor);
        }
    }
}


