using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// A console logger that renders text inside a fixed-size bordered box,
/// supporting colored segments, line wrapping, scrolling, and thread safety.
/// </summary>
public class Logger
{
    // Synchronization object for all console operations
    private readonly object syncRoot = new object();

    // Top-left corner of the entire box (including border)
    private readonly int leftPosition;
    private readonly int topPosition;
    private readonly int totalWidth;   // total width including border
    private readonly int totalHeight;  // total height including border

    private readonly ConsoleColor borderFore;
    private readonly ConsoleColor borderBack;

    // The usable content area (interior) has width = (totalWidth - 2) and height = (totalHeight - 2)
    private readonly int contentWidth;
    private readonly int contentHeight;

    // Each "line" in the buffer is a list of segments, where each segment has its own text and colors.
    private readonly Queue<List<Segment>> bufferLines;

    // A “partial” line under construction (a list of segments). Once you call WriteLine or the
    // accumulated text exceeds contentWidth, these segments get wrapped into one or more full lines.
    private List<Segment> currentSegments;
    private int currentPartialLength; // total number of characters in currentSegments
    private ConsoleColor backColor;
    private ConsoleColor foreColor;

    // Represents a run of text with a single Foreground/Background pair.
    private class Segment
    {
        public string Text;
        public ConsoleColor Fore;
        public ConsoleColor Back;

        public Segment(string text, ConsoleColor fore, ConsoleColor back)
        {
            Text = text;
            Fore = fore;
            Back = back;
        }
    }

    /// <summary>
    /// Constructs a new ConsoleLogger box.
    /// </summary>
    /// <param name="left">Column index (0-based) of the top-left corner of the box.</param>
    /// <param name="top">Row index (0-based) of the top-left corner of the box.</param>
    /// <param name="width">Total width in columns, including the border.</param>
    /// <param name="height">Total height in rows, including the border.</param>
    /// <param name="borderFore">Foreground color for border characters.</param>
    /// <param name="borderBack">Background color for border characters.</param>
    /// <param name="foreColor">Foregrounf color content.</param>
    /// <param name="backColor">Background color for conten.</param>
    /// <param name="sync">Sync object used when writing to console.</param>
    public Logger(int left, int top, int width, int height, ConsoleColor borderFore, ConsoleColor borderBack,
                  ConsoleColor foreColor = ConsoleColor.Black, ConsoleColor backColor = ConsoleColor.White, object? sync = null)
    {
        if (width < 4 || height < 3)
            throw new ArgumentException("Width must be >= 4 and height >= 3 to allow a visible content area.");

        leftPosition = left;
        topPosition = top;
        totalWidth = width;
        totalHeight = height;
        this.borderFore = borderFore;
        this.borderBack = borderBack;
        this.backColor = backColor;
        this.foreColor = foreColor;

        contentWidth = totalWidth - 2;
        contentHeight = totalHeight - 2;

        bufferLines = new Queue<List<Segment>>();
        currentSegments = new List<Segment>();
        currentPartialLength = 0;
        if (sync != null)
            syncRoot = sync;

        DrawBorder();
        ClearContentArea();
    }

    /// <summary>
    /// Draws the box border once. After that, only the interior is redrawn on content changes.
    /// </summary>
    private void DrawBorder()
    {
        lock (syncRoot)
        {
            ConsoleColor prevFore = Console.ForegroundColor;
            ConsoleColor prevBack = Console.BackgroundColor;

            Console.ForegroundColor = borderFore;
            Console.BackgroundColor = borderBack;

            // Draw corners
            PutCharAtInternal('┌', leftPosition, topPosition);
            PutCharAtInternal('┐', leftPosition + totalWidth - 1, topPosition);
            PutCharAtInternal('└', leftPosition, topPosition + totalHeight - 1);
            PutCharAtInternal('┘', leftPosition + totalWidth - 1, topPosition + totalHeight - 1);

            // Top and bottom edges
            for (int dx = 1; dx < totalWidth - 1; dx++)
            {
                PutCharAtInternal('─', leftPosition + dx, topPosition);
                PutCharAtInternal('─', leftPosition + dx, topPosition + totalHeight - 1);
            }

            // Left and right edges
            for (int dy = 1; dy < totalHeight - 1; dy++)
            {
                PutCharAtInternal('│', leftPosition, topPosition + dy);
                PutCharAtInternal('│', leftPosition + totalWidth - 1, topPosition + dy);
            }

            Console.ForegroundColor = prevFore;
            Console.BackgroundColor = prevBack;
        }
    }

    /// <summary>
    /// Clears the interior (content area) by filling it with spaces in the default console colors.
    /// </summary>
    private void ClearContentArea()
    {
        lock (syncRoot)
        {
            ConsoleColor prevFore = Console.ForegroundColor;
            ConsoleColor prevBack = Console.BackgroundColor;

            Console.BackgroundColor = backColor;
            for (int row = 0; row < contentHeight; row++)
            {
                int y = topPosition + 1 + row;
                Console.SetCursorPosition(leftPosition + 1, y);
                Console.Write(new string(' ', contentWidth));
            }

            Console.ForegroundColor = prevFore;
            Console.BackgroundColor = prevBack;
        }
    }

    /// <summary>
    /// Writes a single character at (x, y). Must be called inside a lock on syncRoot.
    /// </summary>
    private void PutCharAtInternal(char c, int x, int y)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(c);
    }

    /// <summary>
    /// Redraws the entire content area: clears it, then prints all buffered lines
    /// (each of which is a list of segments with individual colors).
    /// </summary>
    private void RedrawContent()
    {
        lock (syncRoot)
        {
            // 1) Clear interior
            Console.BackgroundColor = backColor;
            for (int row = 0; row < contentHeight; row++)
            {
                int y = topPosition + 1 + row;
                Console.SetCursorPosition(leftPosition + 1, y);
                Console.Write(new string(' ', contentWidth));
            }

            // 2) Draw each buffered line (up to contentHeight)
            int lineIndex = 0;
            foreach (var lineSegments in bufferLines)
            {
                if (lineIndex >= contentHeight)
                    break;

                int y = topPosition + 1 + lineIndex;
                Console.SetCursorPosition(leftPosition + 1, y);

                int printedSoFar = 0;
                foreach (var seg in lineSegments)
                {
                    Console.ForegroundColor = seg.Fore;
                    Console.BackgroundColor = seg.Back;

                    // If segment would overflow (rare, if wrapping logic errored), truncate:
                    int remainingSpace = contentWidth - printedSoFar;
                    string toWrite = seg.Text.Length <= remainingSpace
                        ? seg.Text
                        : seg.Text.Substring(0, remainingSpace);

                    Console.Write(toWrite);
                    printedSoFar += toWrite.Length;

                    if (printedSoFar >= contentWidth)
                        break;
                }

                // If the line is shorter than contentWidth, pad with spaces in default colors
                Console.BackgroundColor = backColor;
                if (printedSoFar < contentWidth)
                    Console.Write(new string(' ', contentWidth - printedSoFar));

                lineIndex++;
            }

            // Restore default colors
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Splits the list of segments into one “full” line (exactly contentWidth chars) plus remainder.
    /// Returns a tuple: (firstFullLineSegments, leftoverSegments).
    /// </summary>
    private (List<Segment> fullLine, List<Segment> leftover) ExtractFullLine(List<Segment> segments)
    {
        var fullLine = new List<Segment>();
        var leftover = new List<Segment>();

        int countSoFar = 0;

        foreach (var seg in segments)
        {
            if (countSoFar >= contentWidth)
            {
                leftover.Add(new Segment(seg.Text, seg.Fore, seg.Back));
                continue;
            }

            int segLen = seg.Text.Length;
            if (countSoFar + segLen <= contentWidth)
            {
                fullLine.Add(new Segment(seg.Text, seg.Fore, seg.Back));
                countSoFar += segLen;
            }
            else
            {
                int take = contentWidth - countSoFar; // guaranteed > 0
                string firstPart = seg.Text.Substring(0, take);
                string restPart  = seg.Text.Substring(take);

                fullLine.Add(new Segment(firstPart, seg.Fore, seg.Back));
                countSoFar += take;

                leftover.Add(new Segment(restPart, seg.Fore, seg.Back));
            }
        }

        return (fullLine, leftover);
    }

    /// <summary>
    /// Takes whatever is in currentSegments, and enqueues as many full lines (contentWidth chars each)
    /// as possible. Any leftover (shorter than contentWidth) is saved back into currentSegments.
    /// </summary>
    private void WrapCurrentSegmentsIfNeeded()
    {
        while (currentPartialLength > contentWidth)
        {
            var (fullLine, leftover) = ExtractFullLine(currentSegments);
            EnqueueBufferLine(fullLine);

            int leftoverLen = 0;
            foreach (var seg in leftover)
                leftoverLen += seg.Text.Length;

            currentSegments = leftover;
            currentPartialLength = leftoverLen;
        }
    }
    
    /// <summary>
    /// Enqueues one fully-wrapped line (which is a <see cref="List{Segment}"/> whose total length is &lt;= contentWidth).
    /// If buffer is already at capacity, dequeue one line first (scroll).
    /// </summary>
    private void EnqueueBufferLine(List<Segment> lineSegs)
    {
        if (bufferLines.Count >= contentHeight)
            bufferLines.Dequeue();

        bufferLines.Enqueue(lineSegs);
    }

    /// <summary>
    /// Writes text without adding a newline, using default console colors.
    /// Thread-safe: acquires syncRoot.
    /// </summary>
    public void Write(string text)
        => Write(text, foreColor, backColor);

    /// <summary>
    /// Writes text without adding a newline, using default console colors.
    /// Thread-safe: acquires syncRoot.
    /// </summary>
    public void Write(string text, ConsoleColor fore)
        => Write(text, fore, backColor);

    /// <summary>
    /// Writes text without a newline, using the specified colors.
    /// Multiple calls to Write(...) with different fore/back before WriteLine(...) 
    /// will produce multi-colored segments on the same line.
    /// Thread-safe: acquires syncRoot around both buffer manipulation and console redraw.
    /// </summary>
    public void Write(string text, ConsoleColor fore, ConsoleColor back)
    {
        lock (syncRoot)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // 1) Append a new segment
            currentSegments.Add(new Segment(text, fore, back));
            currentPartialLength += text.Length;

            // 2) If total length now exceeds contentWidth, wrap off full lines
            WrapCurrentSegmentsIfNeeded();

            // 3) Redraw (because either partial extended or lines were wrapped)
            RedrawContent();
        }
    }

    /// <summary>
    /// Writes text plus a newline, using default console colors.
    /// Thread-safe: acquires syncRoot.
    /// </summary>
    public void WriteLine(string text)
        => WriteLine(text, foreColor, backColor);

    /// <summary>
    /// Writes text plus a newline, using default console colors.
    /// Thread-safe: acquires syncRoot.
    /// </summary>
    public void WriteLine(string text, ConsoleColor fore)
        => WriteLine(text, fore, backColor);

    /// <summary>
    /// Writes text plus newline, using specified colors. Any pending segments (from previous Write calls)
    /// are combined with this text, then everything is wrapped into as many full lines as needed,
    /// and any remainder (shorter than contentWidth) is enqueued as the final line. After that,
    /// the “currentSegments” buffer is cleared (ready for new writes).
    /// Thread-safe: acquires syncRoot.
    /// </summary>
    public void WriteLine(string text, ConsoleColor fore, ConsoleColor back)
    {
        lock (syncRoot)
        {
            if (text == null)
                text = "";

            // 1) Append this final segment
            currentSegments.Add(new Segment(text, fore, back));
            currentPartialLength += text.Length;

            // 2) While the total length exceeds contentWidth, wrap off full lines
            WrapCurrentSegmentsIfNeeded();

            // 3) Now whatever remains in currentSegments is < contentWidth: enqueue it as the final line
            if (currentPartialLength > 0)
            {
                var finalLineCopy = new List<Segment>(currentSegments);
                EnqueueBufferLine(finalLineCopy);
            }
            else
            {
                // Edge case: if exactly a multiple of contentWidth, currentPartialLength == 0,
                // but we still need to enqueue an empty line to represent the “newline.”
                EnqueueBufferLine(new List<Segment>());
            }

            // 4) Clear for next usage
            currentSegments.Clear();
            currentPartialLength = 0;

            // 5) Redraw
            RedrawContent();
        }
    }

    /// <summary>
    /// Forces the logger to flush any partial line (without adding text or newline). Useful
    /// if you want to ensure the partial line appears even without a WriteLine call.
    /// Thread-safe: acquires syncRoot.
    /// </summary>
    public void FlushPartial()
    {
        lock (syncRoot)
        {
            if (currentPartialLength > 0)
            {
                var finalLineCopy = new List<Segment>(currentSegments);
                EnqueueBufferLine(finalLineCopy);
                currentSegments.Clear();
                currentPartialLength = 0;
                RedrawContent();
            }
        }
    }
}
