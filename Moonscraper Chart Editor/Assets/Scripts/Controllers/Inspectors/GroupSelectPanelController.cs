﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroupSelectPanelController : MonoBehaviour
{
    ChartEditor editor;

	// Use this for initialization
	void Start () {
        editor = ChartEditor.FindCurrentEditor();
	}

    public void SetZeroSustain()
    {
        SetSustain(0);
    }

    public void SetMaxSustain()
    {
        SetSustain(uint.MaxValue);
    }

    void SetSustain(uint length)
    {
        List<ActionHistory.Action> actions = new List<ActionHistory.Action>();
        uint songEndTick = editor.currentSong.TimeToChartPosition(editor.currentSong.length, editor.currentSong.resolution);

        foreach (ChartObject chartObject in editor.currentSelectedObjects)
        {
            if (chartObject.classID == (int)SongObject.ID.Note)
            {
                Note note = chartObject as Note;
                uint assignedLength = length;
                if (length == uint.MaxValue)
                    assignedLength = songEndTick - note.position;

                if (Globals.extendedSustainsEnabled)
                {
                    Note original = (Note)note.Clone();
                    note.sustain_length = assignedLength;
                    note.CapSustain(note.nextSeperateNote);

                    if (original.sustain_length != note.sustain_length)
                        actions.Add(new ActionHistory.Modify(original, note));
                }
                else
                {
                    // Needs to handle chords
                    Note[] chordNotes = note.GetChord();
                    Note[] chordNotesCopy = new Note[chordNotes.Length];

                    for (int i = 0; i < chordNotes.Length; ++i)
                    {
                        chordNotesCopy[i] = (Note)chordNotes[i].Clone();
                        chordNotes[i].sustain_length = assignedLength;
                        note.CapSustain(note.nextSeperateNote);

                        if (chordNotesCopy[i].sustain_length != chordNotes[i].sustain_length)
                            actions.Add(new ActionHistory.Modify(chordNotesCopy[i], chordNotes[i]));
                    }
                }
            }
        }

        if (actions.Count > 0)
            editor.actionHistory.Insert(actions.ToArray());
    }

    public void SetNatural()
    {
        SetNoteType(AppliedNoteType.Natural);
    }

    public void SetStrum()
    {
        SetNoteType(AppliedNoteType.Strum);
    }

    public void SetHopo()
    {
        SetNoteType(AppliedNoteType.Hopo);
    }

    public void SetTap()
    {
        SetNoteType(AppliedNoteType.Tap);
    }

    public void SetNoteType(AppliedNoteType type)
    {
        List<ActionHistory.Action> actions = new List<ActionHistory.Action>();

        foreach (ChartObject note in editor.currentSelectedObjects)
        {
            if (note.classID == (int)SongObject.ID.Note)
            {
                // Need to record the whole chord
                Note unmodified = (Note)note.Clone();
                Note[] chord = ((Note)note).GetChord();

                ActionHistory.Action[] deleteRecord = new ActionHistory.Action[chord.Length];
                for (int i = 0; i < deleteRecord.Length; ++i)
                    deleteRecord[i] = new ActionHistory.Delete(chord[i]);

                SetNoteType(note as Note, type);

                chord = ((Note)note).GetChord();

                ActionHistory.Action[] addRecord = new ActionHistory.Action[chord.Length];
                for (int i = 0; i < addRecord.Length; ++i)
                    addRecord[i] = new ActionHistory.Add(chord[i]);

                if (((Note)note).flags != unmodified.flags)
                {
                    actions.AddRange(deleteRecord);
                    actions.AddRange(addRecord);
                }
            }
        }

        if (actions.Count > 0)
            editor.actionHistory.Insert(actions.ToArray());
    }

    public void SetNoteType(Note note, AppliedNoteType noteType)
    {
        note.flags = Note.Flags.NONE;
        switch (noteType)
        {
            case (AppliedNoteType.Strum):
                if (note.IsChord)
                    note.flags &= ~Note.Flags.FORCED;
                else
                {
                    if (note.IsNaturalHopo)
                        note.flags |= Note.Flags.FORCED;
                    else
                        note.flags &= ~Note.Flags.FORCED;
                }

                break;

            case (AppliedNoteType.Hopo):
                if (!note.CannotBeForcedCheck)
                {
                    if (note.IsChord)
                        note.flags |= Note.Flags.FORCED;
                    else
                    {
                        if (!note.IsNaturalHopo)
                            note.flags |= Note.Flags.FORCED;
                        else
                            note.flags &= ~Note.Flags.FORCED;
                    }
                }
                break;

            case (AppliedNoteType.Tap):
                note.flags |= Note.Flags.TAP;
                break;

            default:
                break;
        }

        note.applyFlagsToChord();

        ChartEditor.editOccurred = true;
    }

    public enum AppliedNoteType
    {
        Natural, Strum, Hopo, Tap
    }
}