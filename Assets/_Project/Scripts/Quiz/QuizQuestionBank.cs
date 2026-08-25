using System.Collections.Generic;

namespace MartProg.Quiz
{
    /// <summary>
    /// Phase 22: static question banks, ported verbatim (question text,
    /// option order, answer index, explanation text — nothing reworded or
    /// re-ordered) from ar_learning_screen.gd's module-keyed arrays, read
    /// directly from source rather than from any prior summary.
    ///
    /// ONE BANK BREAKS THAT RULE. Phase 25d rewrote parts of the Selection
    /// bank and added a mode filter to GetQuestionsForModule(). Source's
    /// Selection questions were found to contradict source's own Selection
    /// module — the Nested If questions describe a single-value "HIGH"
    /// result that the two-value preset rules never produce — and to load
    /// all three modes' questions at once regardless of the mode being
    /// played. Everything about that is documented at the Selection bank
    /// below under "Selection quiz deviations"; nothing else in this file
    /// deviates from source, and the other seven banks are still verbatim.
    ///
    /// Scope decision, read directly from source before deciding: Godot
    /// defines question banks for all 8 modules. Of those, only Array/List,
    /// Stack, Queue, and Linked List are built and reachable in Unity today
    /// (ChangeModuleController only lets a student switch to a module whose
    /// ModuleGroupEntry.IsBuilt is true — see ChangeModuleController.cs note
    /// 2). Functions, Loops, and Selection are included here anyway, fully
    /// ported with complete verified data, for the same reason
    /// ChangeModuleController.cs already lists all 8 modules in its
    /// dropdown rather than just the ones built so far (note 1 there): so
    /// nothing needs to be revisited here once those modules are built —
    /// only QuizController's dispatch needs the module to actually be
    /// reachable, which ChangeModuleController already gates.
    ///
    /// Memory Management's quiz bank is the one genuine exception, DEFERRED
    /// rather than ported now — not merely unreachable like the three
    /// above. Its source, get_memory_activity_quiz_questions(activity_name),
    /// is a dictionary keyed by in-module "activity" name (confirmed:
    /// "Overview" is one such key; reading stopped mid-way through that
    /// function since it clearly continues with more activity keys beyond
    /// it), not a single flat array — porting it faithfully needs a Unity
    /// concept of "current Memory activity" that doesn't exist yet, because
    /// the Memory module itself hasn't been built. Building that mapping now
    /// from an incomplete read would risk silently dropping activities.
    /// This is logged as a known follow-up, matching the project's
    /// established pattern of deferring what depends on not-yet-built
    /// modules (Migration Spec §12).
    ///
    /// When Memory is finally built, note that Selection's Phase 25d filter
    /// was modelled directly on how source already treats Memory — filter by
    /// the active sub-mode, then strip the prefix. Memory's own bank should
    /// therefore need no new mechanism, just its activity name threaded
    /// through GetQuestionsForModule() the same way selectionMode now is.
    /// Memory's questions are also worth checking for the same staleness
    /// Selection's turned out to have.
    /// </summary>
    public static class QuizQuestionBank
    {
        // -----------------------------------------------------------------
        // Array / List — array_quiz_questions (ar_learning_screen.gd)
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> ArrayQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                "In most programming languages, what is the index of the first element in an array or list?",
                new[] { "0", "1", "-1", "10" }, 0,
                "Array/list indexing usually starts from 0. Therefore, the first element is at index 0."),
            new QuizQuestion(
                "Given numbers = [10, 20, 30], what is the value at index 1?",
                new[] { "10", "20", "30", "1" }, 1,
                "Index 0 contains 10, index 1 contains 20, and index 2 contains 30."),
            new QuizQuestion(
                "Given numbers = [10, 20, 30], what is the index of the value 30?",
                new[] { "0", "1", "2", "3" }, 2,
                "The value 30 is the third item, but because indexing starts from 0, its index is 2."),
            new QuizQuestion(
                "What does numbers.append(40) do in Python?",
                new[] { "Removes 40 from the list", "Adds 40 to the end of the list", "Prints 40", "Searches for 40" }, 1,
                "append() adds a new value to the end of a Python list."),
            new QuizQuestion(
                "After numbers = [10, 20, 30] and numbers.append(40), what is the new list?",
                new[] { "[40, 10, 20, 30]", "[10, 20, 30]", "[10, 20, 30, 40]", "[10, 40, 20, 30]" }, 2,
                "append(40) adds 40 after the last existing value, so the list becomes [10, 20, 30, 40]."),
            new QuizQuestion(
                // Phase 28: reframed from "in this module" to "in Python".
                // Q4 above already asks "...in Python?"; this one broke the
                // parallel. Options and answer index unchanged.
                "What does numbers.pop() do in Python?",
                new[] { "Removes the first value", "Removes the last value", "Adds a new value", "Sorts the list" }, 1,
                "pop() with no argument removes and returns the last value in a Python list."),
            new QuizQuestion(
                "Given numbers = [10, 20, 30, 40], what value is removed by numbers.pop()?",
                new[] { "10", "20", "30", "40" }, 3,
                "The last value is 40, so numbers.pop() removes 40."),
            new QuizQuestion(
                "What does traversal mean in an array or list?",
                new[] { "Deleting all values", "Adding a new value", "Visiting each value one after another", "Changing the name of the list" }, 2,
                "Traversal means moving through the array/list and visiting each value one by one."),
            new QuizQuestion(
                "Given numbers = [10, 20, 30], what is the output of traversing the list from left to right?",
                new[] { "30 → 20 → 10", "10 → 20 → 30", "0 → 1 → 2", "10 → 30 → 20" }, 1,
                "Traversing from left to right visits the values in their stored order: 10, then 20, then 30."),
            new QuizQuestion(
                "If the value 20 is searched in numbers = [10, 20, 30], what index should be returned?",
                new[] { "0", "1", "2", "Value not found" }, 1,
                "The value 20 is the second item in the list, so its index is 1."),
        };

        // -----------------------------------------------------------------
        // Stack — stack_quiz_questions (ar_learning_screen.gd)
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> StackQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                "What rule does a stack follow?",
                new[] { "First-In, First-Out", "Last-In, First-Out", "Random access", "Smallest value first" }, 1,
                "A stack follows Last-In, First-Out. The last value added is the first value removed."),
            new QuizQuestion(
                "Which stack operation adds a value to the top of the stack?",
                new[] { "Pop", "Peek", "Push", "Search" }, 2,
                "Push adds a new value to the top of the stack."),
            new QuizQuestion(
                "Which stack operation removes the top value?",
                new[] { "Push", "Pop", "Peek", "Reset" }, 1,
                "Pop removes the value currently at the top of the stack."),
            new QuizQuestion(
                "Given stack = [10, 20, 30], which value is at the top?",
                new[] { "10", "20", "30", "0" }, 2,
                "The last value in the stack is 30, so 30 is the top value."),
            new QuizQuestion(
                "Given stack = [10, 20, 30], what value is removed by pop()?",
                new[] { "10", "20", "30", "All values" }, 2,
                "pop() removes the top value. In this stack, the top value is 30."),
            new QuizQuestion(
                "What does peek do in a stack?",
                new[] { "Removes the top value", "Adds a value", "Views the top value without removing it", "Deletes the whole stack" }, 2,
                "Peek shows the top value without removing it from the stack."),
            new QuizQuestion(
                "After pushing 40 onto stack = [10, 20, 30], what is the new top value?",
                new[] { "10", "20", "30", "40" }, 3,
                "Push adds 40 to the top, so 40 becomes the new top value."),
            new QuizQuestion(
                "If only one value remains in the stack, what is true?",
                new[] { "It has no top", "It has no bottom", "The value is both top and bottom", "It cannot be popped" }, 2,
                "When one value remains, that value is both the top and the bottom of the stack."),
            new QuizQuestion(
                "Which operation should be used to check the top value without deleting it?",
                new[] { "Push", "Pop", "Peek", "Reset" }, 2,
                "Peek is used to view the top value without removing it."),
            new QuizQuestion(
                "Why is a stack called Last-In, First-Out?",
                new[] { "The first value is always removed first", "The last value added is removed first", "Values are sorted before removal", "Values are removed randomly" }, 1,
                "The most recently pushed value is the first one removed by pop()."),
        };

        // -----------------------------------------------------------------
        // Queue — queue_quiz_questions (ar_learning_screen.gd)
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> QueueQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                "What rule does a queue follow?",
                new[] { "Last-In, First-Out", "First-In, First-Out", "Random access", "Highest value first" }, 1,
                "A queue follows First-In, First-Out. The first value added is the first value removed."),
            new QuizQuestion(
                "Which queue operation adds a value to the rear of the queue?",
                new[] { "Dequeue", "Peek", "Enqueue", "Reset" }, 2,
                "Enqueue adds a new value to the rear of the queue."),
            new QuizQuestion(
                "Which queue operation removes the front value?",
                new[] { "Enqueue", "Dequeue", "Peek", "Search" }, 1,
                "Dequeue removes the value at the front of the queue."),
            new QuizQuestion(
                "Given queue = [10, 20, 30], which value is at the front?",
                new[] { "10", "20", "30", "0" }, 0,
                "The first value in the queue is 10, so 10 is at the front."),
            new QuizQuestion(
                "Given queue = [10, 20, 30], which value is at the rear?",
                new[] { "10", "20", "30", "1" }, 2,
                "The last value in the queue is 30, so 30 is at the rear."),
            new QuizQuestion(
                "Given queue = [10, 20, 30], what value is removed by dequeue?",
                new[] { "10", "20", "30", "All values" }, 0,
                "Dequeue removes the front value. In this queue, the front value is 10."),
            new QuizQuestion(
                "After enqueueing 40 into queue = [10, 20, 30], what is the new queue?",
                new[] { "[40, 10, 20, 30]", "[10, 20, 30, 40]", "[10, 40, 20, 30]", "[10, 20, 40, 30]" }, 1,
                "Enqueue adds the new value to the rear, so the queue becomes [10, 20, 30, 40]."),
            new QuizQuestion(
                "What does peek do in a queue?",
                new[] { "Removes the front value", "Adds a new value", "Views the front value without removing it", "Deletes the queue" }, 2,
                "Peek views the front value without removing it from the queue."),
            new QuizQuestion(
                "Which Python expression can view the front value of a list-based queue?",
                new[] { "queue[-1]", "queue[0]", "queue.append()", "queue.pop()" }, 1,
                "queue[0] reads the first value in the list, which represents the front of the queue."),
            new QuizQuestion(
                // Phase 28: reframed from "in this queue module" to the general
                // case, matching Q9's phrasing above. Options unchanged.
                "Which Python command removes the front value from a list-based queue?",
                new[] { "queue.append(value)", "queue.pop()", "queue.pop(0)", "queue[-1]" }, 2,
                "queue.pop(0) removes the first value in the list, which represents the front of the queue. queue.pop() with no argument removes the last item - the rear."),
        };

        // -----------------------------------------------------------------
        // Linked List — linked_list_quiz_questions (ar_learning_screen.gd)
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> LinkedListQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                // Phase 28: all three distractors named other modules in this
                // app (Array/List "Indexes", "Stacks", "Queues"), so a student
                // eliminated them by recognising the vocabulary rather than by
                // knowing what a linked list is. Replaced.
                "What is a linked list made of?",
                new[] { "A single continuous block of memory", "Nodes, each storing a value and a link to the next node", "Sorted numeric values only", "Fixed-size slots that cannot be added to" }, 1,
                "A linked list is made of nodes. Each node stores data and a link to the next node - unlike an array, the nodes need not sit in one continuous block of memory."),
            new QuizQuestion(
                // Phase 28: "Rear" (Queue) and "Top" (Stack) were borrowed from
                // sibling modules. Replaced with same-domain distractors.
                "What is the first node in a linked list called?",
                new[] { "Tail", "Head", "Root", "Origin" }, 1,
                "The first node in a linked list is called the head."),
            new QuizQuestion(
                // Phase 28: "Front" (Queue) and "Index" (Array/List) were
                // borrowed from sibling modules. Replaced.
                "What is the last node in a linked list called?",
                new[] { "Head", "Tail", "Root", "Leaf" }, 1,
                "The last node in a linked list is called the tail. It is the node whose next link is null."),
            new QuizQuestion(
                "In a singly linked list, what does each node point to?",
                new[] { "The previous node", "The next node", "A random node", "The array index" }, 1,
                "In a singly linked list, each node points to the next node in the list."),
            new QuizQuestion(
                "What does NULL mean at the end of a linked list?",
                new[] { "The list starts again", "There is no next node", "The node has no value", "The list is sorted" }, 1,
                "NULL means the tail node does not point to another node. It marks the end of the linked list."),
            new QuizQuestion(
                "Given head → [10] → [20] → [30] → null, what is the head value?",
                new[] { "10", "20", "30", "null" }, 0,
                "The head is the first node, so the head value is 10."),
            new QuizQuestion(
                "Given head → [10] → [20] → [30] → null, what is the tail value?",
                new[] { "10", "20", "30", "head" }, 2,
                "The tail is the last node before null, so the tail value is 30."),
            new QuizQuestion(
                // Phase 28: "It removes the head" and "It deletes the list" are
                // incoherent as answers to an INSERT question, so a student
                // eliminated them by verb rather than by knowledge. Replaced
                // with plausible insert positions. The premise was verified
                // correct against source: insert_value() appends and its
                // message reads "inserted at the tail of the linked list".
                "What happens when a new node is inserted at the tail?",
                new[] { "It becomes the new head", "It is added after the current tail, and becomes the new tail", "It is added before the current head", "It is inserted halfway along the list" }, 1,
                "Inserting at the tail adds the new node after the current tail, so the new node becomes the tail and its next link is null."),
            new QuizQuestion(
                "What happens when the head node is deleted?",
                new[] { "The tail becomes null", "The head moves to the next node", "All nodes are deleted", "The list becomes a stack" }, 1,
                "Deleting the head means the head reference moves to the next node."),
            new QuizQuestion(
                // Phase 28: "jumping directly to any index" (Array/List) and
                // "From rear to front" (Queue) were sibling-module vocabulary;
                // "Only from the middle" was absurd. Replaced.
                "How is a linked list usually traversed?",
                new[] { "By accessing any node directly without following links", "From head to tail by following each next link", "From tail to head by following links backward", "By visiting nodes in random order" }, 1,
                "A linked list is traversed from head to tail by following each next link. Unlike an array it has no direct-index access, so reaching the nth node means passing through all the ones before it."),
        };

        // -----------------------------------------------------------------
        // Functions — functions_quiz_questions (ar_learning_screen.gd)
        // Not yet reachable — Functions module isn't built in Unity yet.
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> FunctionsQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                // Phase 28: "A type of queue", "A memory address" and "A loop
                // condition" were lifted from the Queue, Memory and Loops
                // modules. Replaced with things a function is genuinely
                // confused with.
                "What is a function in programming?",
                new[] { "A reusable block of code designed to perform a specific task", "A single variable that stores one value", "A comment that documents code but does not run", "A fixed value that never changes while the program runs" }, 0,
                "A function is a reusable block of code designed to perform a specific task - unlike a variable, a comment, or a constant."),
            new QuizQuestion(
                "In the function add(a, b), what are a and b called?",
                new[] { "Indexes", "Parameters", "Queues", "Return values" }, 1,
                "a and b are parameters. They receive values when the function is called."),
            new QuizQuestion(
                "Given def add(a, b): return a + b, what does add(10, 20) return?",
                new[] { "10", "20", "30", "200" }, 2,
                "The function adds 10 and 20, so it returns 30."),
            new QuizQuestion(
                "What does the return statement do in a function?",
                new[] { "Stops the program permanently", "Sends a value back from the function", "Deletes the function", "Creates a loop" }, 1,
                "The return statement sends a value back to the place where the function was called."),
            new QuizQuestion(
                "In result = add(10, 20), what is result?",
                new[] { "A parameter", "A variable storing the returned value", "A function name", "An error message" }, 1,
                "result stores the value returned by the function call add(10, 20)."),
            new QuizQuestion(
                "Which part is the function call?",
                new[] { "def add(a, b):", "return a + b", "add(10, 20)", "a + b" }, 2,
                "add(10, 20) is the function call because it runs the function with the values 10 and 20."),
            new QuizQuestion(
                "What is the function name in def add(a, b):?",
                new[] { "def", "add", "a", "b" }, 1,
                "The function name is add."),
            new QuizQuestion(
                // Phase 28: distractors were absurd or borrowed from Array/List
                // ("They only work with arrays"). Replaced with statements that
                // are wrong about functions specifically.
                "Why are functions useful?",
                new[] { "The same logic can be written once and reused many times", "They must be rewritten each time they are needed", "They make a program run without any variables", "They can only be used once per program" }, 0,
                "Functions let one piece of logic be written once and called as often as needed, which keeps a program shorter and easier to change."),
            new QuizQuestion(
                // Phase 28: "The queue is searched" was Queue vocabulary.
                // Replaced with wrong orderings of the call sequence, which is
                // what this question is actually testing.
                "What happens first when add(10, 20) is called?",
                new[] { "The values 10 and 20 are passed into the parameters a and b", "The function returns a result before its code runs", "The function's parameters are deleted", "The function's code is skipped entirely" }, 0,
                "A call first binds the arguments to the parameters; the body runs afterwards, and only then is a value returned."),
            new QuizQuestion(
                // Phase 28: the worst case found in the whole audit. All three
                // distractors were method calls from Queue, Stack and
                // Array/List, so the keyed answer was the ONLY option
                // containing "def" - identifiable by shape with no Python
                // knowledge at all. Replaced with near-miss definitions.
                "Which statement correctly defines a function that adds two values?",
                new[] { "def add(a, b): return a + b", "def add(a, b) return a + b", "add(a, b): return a + b", "def add(a, b): print(a + b)" }, 0,
                "A correct definition needs def, a colon after the parameter list, and return to send the sum back. The others are missing the colon, missing def, or print the sum instead of returning it."),
        };

        // -----------------------------------------------------------------
        // Loops — loop_quiz_questions (ar_learning_screen.gd)
        // Not yet reachable — Loops module isn't built in Unity yet.
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> LoopQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                "What does a loop do in programming?",
                new[] { "Repeats a block of code", "Deletes a list", "Stores only one value", "Stops the program" }, 0,
                "A loop repeats a block of code while moving through a sequence or while a condition is true."),
            new QuizQuestion(
                "In the code for value in numbers:, what does value represent?",
                new[] { "The current item being visited", "The final total", "The loop name", "The list index only" }, 0,
                "value represents the current item from the list during each loop iteration."),
            new QuizQuestion(
                "Given numbers = [10, 20, 30], how many times will for value in numbers run?",
                new[] { "1", "2", "3", "4" }, 2,
                "The list has three values, so the loop runs three times."),
            new QuizQuestion(
                "What is the starting value of total in total = 0?",
                new[] { "0", "10", "20", "30" }, 0,
                "total starts at 0 before the loop begins adding values."),
            new QuizQuestion(
                "What does total += value mean?",
                new[] { "Add value to total", "Remove value from total", "Set total to 0", "Skip the current value" }, 0,
                "total += value is short for total = total + value."),
            new QuizQuestion(
                "After visiting 10 in [10, 20, 30], what is the running total?",
                new[] { "0", "10", "20", "30" }, 1,
                "The first value is 10, so the running total becomes 10."),
            new QuizQuestion(
                "After visiting 10 and 20 in [10, 20, 30], what is the running total?",
                new[] { "10", "20", "30", "60" }, 2,
                "10 + 20 = 30, so the running total becomes 30."),
            new QuizQuestion(
                "What is the final total after looping through [10, 20, 30]?",
                new[] { "30", "40", "50", "60" }, 3,
                "The loop adds 10 + 20 + 30, giving a final total of 60."),
            new QuizQuestion(
                // Phase 28: was a question about a BUTTON, and its three
                // distractors described the other buttons on the same screen -
                // answerable from thirty seconds of using the app without
                // knowing what a loop is. Exactly what Phase 25d rejected for
                // Selection. Replaced with a question about loop iteration.
                "In a for loop such as for value in numbers:, what happens during a single iteration?",
                new[] { "The loop processes the current item, then advances to the next item in the sequence", "The loop processes every item in the list at the same time", "The loop restarts the list from the first item", "The loop exits the program immediately" }, 0,
                "One iteration handles exactly one item from the sequence before moving on; the loop repeats until every item has been visited."),
            new QuizQuestion(
                // Phase 28: same defect as the question above. Replaced with a
                // question about loop termination.
                "A for loop such as for value in numbers: stops running when which condition is met?",
                new[] { "Every item in the sequence has been visited", "The running total reaches a fixed target number", "The programmer manually stops the loop each time", "The list becomes sorted into ascending order" }, 0,
                "A for loop over a collection terminates once it has iterated through every item - no external stopping condition is needed."),
        };

        // -----------------------------------------------------------------
        // Selection — get_selection_quiz_questions() (ar_learning_screen.gd)
        //
        // *** THE ONLY BANK IN THIS FILE THAT IS NOT A PORT. It is a
        // *** REPLACEMENT. Read "Selection quiz deviations" below before
        // *** diffing any of it against source.
        //
        // Source's version is a GENERATOR FUNCTION, not a static array
        // (unlike every bank above it) — it takes no parameters and always
        // returns the same 13 questions.
        //
        // ----- Selection quiz deviations (Phase 25d) -----
        //
        // Three problems were found once the Selection module was actually
        // playable in Unity. All three are DELIBERATE DEVIATIONS here rather
        // than faithful ports. This is the largest deviation in the
        // migration; it is written up in the migration spec too.
        //
        // (a) NO MODE FILTER IN SOURCE. load_quiz_for_current_module() does
        //     `quiz_questions = get_selection_quiz_questions().duplicate(true)`
        //     then .shuffle() — all 13 questions load regardless of which
        //     Selection mode is being played, which is why each question
        //     carries a literal "Mode: X\n\n" prefix in its text. Playing in
        //     Basic If and being asked a Nested If question was reported
        //     from device as a bug, and it is one pedagogically even though
        //     it is what source does.
        //
        //     Source is internally inconsistent about this: Memory is the
        //     only other module with sub-modes, and it DOES filter —
        //     get_memory_activity_quiz_questions(current_memory_activity) —
        //     and strips its prefix in clean_quiz_question_text(). Selection
        //     got neither. GetQuestionsForModule() below now applies the
        //     Memory treatment to Selection: filter by the active mode, then
        //     strip the now-redundant prefix.
        //
        // (b) THE CONTENT WAS STALE AGAINST SOURCE'S OWN MODULE. The Else-if
        //     and Nested If questions were written against an older Selection
        //     implementation and never updated when the preset-rule system
        //     landed:
        //
        //     - Else-if: explanations cited `value >= 85`. The actual
        //       get_current_ladder_rule() Grade Rule branches at
        //       score >= 80 / 70 / 50. A score of 82 is "A" in the module
        //       while the old explanation implied "B".
        //
        //     - Nested If: all four questions assumed ONE value and a "HIGH"
        //       result (outer value >= 50, inner value >= 75 -> HIGH / PASS /
        //       FAIL). get_current_nested_rule() takes TWO values and returns
        //       Pass / "Attendance too low" / Fail, or Eligible / "Project
        //       score too low" / "Exam score too low". The string "HIGH"
        //       appears nowhere in the module. Three of those four questions
        //       could not be answered by playing the module at all.
        //
        // (c) THE QUESTIONS TESTED THE APP, NOT THE CONCEPT. Several asked
        //     about this application's own controls rather than about
        //     selection logic — "When should the result change from ? to
        //     TRUE or FALSE?" with the answer "After clicking Check", and
        //     similar. A student who never opened the app could not answer
        //     them, and a student who did learned nothing transferable about
        //     conditionals. For a module whose stated purpose is teaching
        //     programming concepts, that is backwards.
        //
        //     Every question is therefore rewritten to be ACADEMIC: it asks
        //     about if/else semantics, operator behaviour, evaluation order
        //     and equivalence, using language-neutral pseudocode. No question
        //     names a button, a panel, or anything else specific to this
        //     application.
        //
        //     They remain GROUNDED in the module: the pseudocode in each
        //     question is the logic the module actually visualizes, and the
        //     thresholds and result strings are the module's own (from
        //     get_current_ladder_rule() and get_current_nested_rule()). A
        //     student can therefore work an answer out by reasoning, and then
        //     watch the module confirm it — but the reasoning is the point,
        //     and it transfers to any language.
        //
        // Bank size: 13 -> 18, an even SIX PER MODE (requested), so no mode's
        // quiz is shorter than another's once the filter in (a) is applied.
        //
        // The "Mode: X\n\n" prefixes are RETAINED in the data even though
        // they are stripped before display — they are what the filter matches
        // on.
        //
        // Answer index is 0 for every question. That is not a leak:
        // QuizController.RandomizeQuizOptions() shuffles each question's
        // options at load time and rewrites the index, exactly as source's
        // randomize_quiz_options() does. Authoring correct-answer-first keeps
        // the bank readable.
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> SelectionQuizQuestions = new List<QuizQuestion>
        {
            // ----- Basic If: boolean evaluation and if/else semantics -----
            new QuizQuestion(
                "Mode: Basic If\n\nGiven value = 45, what does the expression value >= 50 evaluate to?",
                new[] { "false", "true", "45", "It causes an error" }, 0,
                "45 is less than 50, so the comparison is not satisfied and the expression evaluates to false. A comparison always produces a boolean, never the number being compared."),
            new QuizQuestion(
                "Mode: Basic If\n\nGiven value = 50, what does the expression value >= 50 evaluate to?",
                new[] { "true", "false", "Nothing — 50 is the boundary", "It depends on the language" }, 0,
                "The >= operator is true when the left side is greater than OR equal to the right side, so the boundary value itself satisfies it. This is the difference between >= and >, and it is the most common off-by-one mistake in conditions."),
            new QuizQuestion(
                "Mode: Basic If\n\nGiven count = 0, what does the expression count != 0 evaluate to?",
                new[] { "false", "true", "0", "1" }, 0,
                "!= means \"not equal to\". count is 0, so count != 0 is false. Its opposite, count == 0, is true for the same value."),
            new QuizQuestion(
                "Mode: Basic If\n\nIn the statement:\n\nif condition:\n   A\nelse:\n   B\n\nwhen does B run?",
                new[] { "Only when condition is false", "Always, after A", "Only when condition is true", "Only when condition causes an error" }, 0,
                "The else branch is the alternative to the if branch. It runs on exactly the cases the condition did not cover, so for any single evaluation either A runs or B runs — never both, and never neither."),
            new QuizQuestion(
                "Mode: Basic If\n\nWhich operator tests whether two values are EQUAL in most C-style languages?",
                new[] { "==", "=", "=>", "!=" }, 0,
                "== compares. A single = assigns a value to a variable, which is why writing if (x = 5) is a classic bug: it assigns 5 to x rather than testing x against 5. != tests for inequality."),
            new QuizQuestion(
                "Mode: Basic If\n\nWhat kind of value must the condition of an if statement produce?",
                new[] { "A boolean — true or false", "An integer", "A string", "Any value at all" }, 0,
                "A condition is a question with a yes/no answer, so it must reduce to a boolean. Some languages will additionally treat certain non-boolean values as true or false, but the underlying decision is always boolean."),

            // ----- Else-if Ladder: first-match-wins and ordering -----
            new QuizQuestion(
                "Mode: Else-if Ladder\n\nGiven this ladder:\n\nif score >= 80: A\nelse if score >= 70: B\nelse if score >= 50: Pass\nelse: Fail\n\nWhat does score = 75 produce?",
                new[] { "B", "A", "Pass", "Fail" }, 0,
                "75 fails the first condition (75 >= 80 is false) but satisfies the second (75 >= 70), so the ladder stops there and produces B."),
            new QuizQuestion(
                "Mode: Else-if Ladder\n\nUsing the same ladder (>= 80 A, >= 70 B, >= 50 Pass, else Fail), what does score = 80 produce?",
                new[] { "A", "B", "Pass", "Fail" }, 0,
                "80 >= 80 is true, because >= includes equality. The very first branch matches, so the result is A. Boundary values belong to the branch whose operator includes them."),
            new QuizQuestion(
                "Mode: Else-if Ladder\n\nUsing the same ladder (>= 80 A, >= 70 B, >= 50 Pass, else Fail), what does score = 40 produce?",
                new[] { "Fail", "Pass", "B", "Nothing is produced" }, 0,
                "40 satisfies none of the three conditions, so control reaches the final else. That is what the else is for: it guarantees the ladder always produces some result."),
            new QuizQuestion(
                "Mode: Else-if Ladder\n\nOnce one condition in an else-if ladder evaluates to true, what happens to the conditions below it?",
                new[] { "They are skipped without being evaluated", "They are all still evaluated", "They are evaluated but their results are discarded", "They are evaluated in reverse order" }, 0,
                "An else-if ladder is first-match-wins. Later conditions are attached to the failure of every earlier one, so once a branch is taken the rest are never even tested."),
            new QuizQuestion(
                "Mode: Else-if Ladder\n\nSuppose the same ladder were reordered so that score >= 50 was tested FIRST. What would score = 95 then produce?",
                new[] { "Pass", "A", "B", "Fail" }, 0,
                "95 >= 50 is true, so the first branch would match and the ladder would stop there — returning Pass for a score that deserves A. Because the first true condition wins, a ladder of overlapping ranges must be ordered from the most specific (or highest) to the least."),
            new QuizQuestion(
                "Mode: Else-if Ladder\n\nWhat is the purpose of the final else in an else-if ladder?",
                new[] { "It handles every case no earlier condition matched", "It repeats the last condition", "It is required by the language", "It runs in addition to whichever branch matched" }, 0,
                "The final else is the catch-all, or default. Without it, a value matching none of the conditions produces no result at all — which is a common source of silent bugs."),

            // ----- Nested If: inner reachability and equivalence -----
            new QuizQuestion(
                "Mode: Nested If\n\nGiven this nested statement:\n\nif score >= 50:\n   if attendance >= 75: Pass\n   else: Attendance too low\nelse: Fail\n\nWhat does score = 65, attendance = 80 produce?",
                new[] { "Pass", "Attendance too low", "Fail", "Both Pass and Fail" }, 0,
                "The outer condition 65 >= 50 is true, so the inner condition is evaluated. 80 >= 75 is also true, so the result is Pass. Both conditions had to hold."),
            new QuizQuestion(
                "Mode: Nested If\n\nUsing the same nested structure, what does score = 65, attendance = 50 produce?",
                new[] { "Attendance too low", "Pass", "Fail", "Nothing is produced" }, 0,
                "The outer condition is true, so the inner one is evaluated — and 50 >= 75 is false, so the inner else runs. Note this is a different outcome from failing the outer condition, which is exactly what nesting buys you: it distinguishes WHY the value failed."),
            new QuizQuestion(
                "Mode: Nested If\n\nUsing the same nested structure, what does score = 40, attendance = 90 produce?",
                new[] { "Fail", "Pass", "Attendance too low", "It depends on the attendance" }, 0,
                "The outer condition 40 >= 50 is false, so the entire inner statement is skipped and the attendance of 90 is never examined. A value inside a nested block is only relevant once the outer condition has already been satisfied."),
            new QuizQuestion(
                "Mode: Nested If\n\nWhen is the inner condition of a nested if statement evaluated?",
                new[] { "Only after the outer condition is true", "Before the outer condition", "Always, regardless of the outer condition", "Only when the outer condition is false" }, 0,
                "Nesting places the inner statement inside the outer one's true branch, so reaching it requires the outer condition to have already succeeded. This is what makes nested conditions a sequence of gates rather than a set of independent tests."),
            new QuizQuestion(
                "Mode: Nested If\n\nHow many distinct outcomes can this structure produce?\n\nif A:\n   if B: X\n   else: Y\nelse: Z",
                new[] { "Three", "Two", "Four", "It depends on A and B" }, 0,
                "The reachable outcomes are X (A and B both true), Y (A true, B false) and Z (A false). There is no fourth: when A is false, B is never evaluated, so \"A false and B true\" is not a separate case."),
            new QuizQuestion(
                "Mode: Nested If\n\nIf there are NO else branches, which single statement is equivalent to:\n\nif A:\n   if B:\n      X",
                new[] { "if A AND B -> X", "if A OR B -> X", "if A -> X", "They can never be made equivalent" }, 0,
                "With no else branches, nesting two conditions is logically the same as combining them with AND — X runs only when both hold. The nested form earns its keep when the outer-false case needs its own handling, because AND collapses \"A failed\" and \"B failed\" into one indistinguishable outcome."),
        };

        // -----------------------------------------------------------------
        // Memory Management — get_memory_activity_quiz_questions(activity_name)
        //
        // Source's only DICTIONARY-shaped bank: keyed by activity name rather
        // than a flat array, because Memory has fourteen activities and each
        // gets its own five questions. That structure is why this bank was
        // deferred in Phase 22 — there was no Unity concept of "current
        // Memory activity" to key it by until Phase 26 built one.
        //
        // ----- Audit result and rewrite (Phase 26f) -----
        //
        // Audited before porting, per the standing decision after Selection's
        // bank turned out to be both stale and app-oriented. Two findings:
        //
        // 1. **Staleness is mild — unlike Selection's.** Nothing here is
        //    unanswerable. There is no equivalent of Selection's "HIGH"
        //    result that the module never produces. Two mismatches only:
        //    Loop Memory asked about numbers = [10, 20, 30] where the
        //    activity traces {2, 4, 6}, and Scope asked where a variable
        //    declared inside a FUNCTION is accessible, where the activity
        //    demonstrates an if-BLOCK (function frames are the Functions
        //    activity's subject). Both corrected against what the module
        //    shows.
        //
        // 2. **24 of the 65 tested the app, not memory.** "If the learner
        //    types only age, what should the module do?", "Why does the
        //    module show array indexes?", "What should the memory table show
        //    after removal?", "Which learning action is most supported by
        //    this module?" — and Overview was 5 of 5. All rewritten as
        //    memory-concept questions at the user's decision, Overview
        //    included, reframed around how memory itself is modelled:
        //    records, addresses, name-versus-address, consecutive layout, and
        //    what does and does not change on assignment.
        //
        // 3. **The DISTRACTORS were the bigger problem, and were not in the
        //    original brief.** Across all thirteen banks the wrong answers
        //    were app furniture — "A quiz panel", "The button colour", "The
        //    screen size", "The student ID only", "Stops the whole module
        //    permanently". A student who knows nothing about memory can
        //    eliminate three of four options on sight, which makes the
        //    question measure nothing. Every distractor is now a plausible
        //    programming claim, so the questions have to be answered rather
        //    than filtered. This is why the rewrite touches all 65 entries
        //    and not only the 24.
        //
        // Grounding: where an activity demonstrates specific code, its
        // questions now use THAT code — Loop Memory's {2, 4, 6} summing to
        // 12, Scope's x-outside/y-inside block, Selection's score = 45 else
        // branch, and three of Debug's questions drawn from its own four
        // exercises. A student can reason an answer out and then watch the
        // activity confirm it.
        //
        // Counts unchanged: five per activity, "Final Assessment" empty, 65
        // total.
        //
        // The "{Activity}\n\n" prefix is retained in the data and stripped
        // before display by CleanMemoryQuestion(). Note Overview's prefix is
        // "Memory Overview", not "Overview" — source's own asymmetry, which
        // is exactly why clean_quiz_question_text() carries a special case
        // for it.
        // -----------------------------------------------------------------
        public static readonly Dictionary<string, List<QuizQuestion>> MemoryQuizQuestions =
            new Dictionary<string, List<QuizQuestion>>
        {
            { "Overview", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Memory Overview\n\nIn a memory model, what does one memory record represent?",
                    new[] { "One named storage location, with its address, type, value and size", "One line of source code", "One function in the program", "One value that has been printed" }, 0,
                    "A record is one place in memory: a name, the address it occupies, the type it holds, its current value, and how many bytes it uses."),
                new QuizQuestion(
                    "Memory Overview\n\nWhat is a memory address?",
                    new[] { "The numbered location where a value is stored", "The value currently stored", "The name the programmer gave the variable", "The number of bytes the type needs" }, 0,
                    "An address identifies WHERE a value lives. The name, the value and the size are separate properties of that same location."),
                new QuizQuestion(
                    "Memory Overview\n\nA variable name and its memory address refer to what?",
                    new[] { "The same storage location - one by name, one by number", "Two locations that happen to hold the same value", "The value and its type", "The start and the end of the program" }, 0,
                    "A name is a convenient label for an address. Both identify one location."),
                new QuizQuestion(
                    "Memory Overview\n\nWhy is memory usually pictured as consecutive numbered locations?",
                    new[] { "Because each value occupies a known number of bytes, one after another", "Because values are stored in alphabetical order", "Because every value occupies exactly one byte", "Because the order of declaration has no effect on layout" }, 0,
                    "Storage is laid out in order, and each type's size determines where the next value begins."),
                new QuizQuestion(
                    "Memory Overview\n\nWhen a new value is stored in an existing variable, what stays the same?",
                    new[] { "Its name, type and address", "Only its name", "Nothing - a new location is created", "Its value, but not its address" }, 0,
                    "Assignment replaces the contents of a location. The location itself does not move."),
            } },

            { "Variables", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Variables\n\nWhat happens when int age = 20 is executed?",
                    new[] { "A memory location is allocated for age and stores 20", "The value 20 is stored without reserving a location", "age is compared with 20", "20 is stored under the name int" }, 0,
                    "A variable declaration creates a memory record and stores the assigned value."),
                new QuizQuestion(
                    "Variables\n\nWhat happens when age = 25 runs after int age = 20?",
                    new[] { "The existing value of age is updated to 25", "A second variable named age is created", "age keeps 20 because it was already initialised", "age's address changes to hold the new value" }, 0,
                    "Assignment updates the value stored in the existing variable. The location does not change."),
                new QuizQuestion(
                    "Variables\n\nReading a variable's value without assigning to it has what effect on memory?",
                    new[] { "None - the stored value is unchanged", "The value is cleared once it has been read", "The value is copied to a new address", "The variable's type is inferred again" }, 0,
                    "Reading is not writing. A value can be read any number of times and remains as it was."),
                new QuizQuestion(
                    "Variables\n\nWhich part of int score = 70 is the variable name?",
                    new[] { "score", "int", "70", "=" }, 0,
                    "score is the identifier used to refer to the memory location. int is the type and 70 is the value."),
                new QuizQuestion(
                    "Variables\n\nWhat is the difference between declaration and assignment?",
                    new[] { "Declaration creates the variable; assignment stores or changes its value", "Declaration stores a value; assignment reserves the location", "They are two words for the same operation", "Declaration can only ever happen once per program" }, 0,
                    "Declaration creates the memory record. Assignment gives or changes the value stored in it."),
            } },

            { "Data Types", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Data Types\n\nWhat does a data type define?",
                    new[] { "The kind of value a location can hold and how much memory it uses", "Only the name the variable is given", "The order in which variables are declared", "The address the value is stored at" }, 0,
                    "A data type defines both what may be stored - int, float, char, bool, string - and how many bytes that takes."),
                new QuizQuestion(
                    "Data Types\n\nWhich value best represents a char?",
                    new[] { "'A'", "15.5", "true", "200" }, 0,
                    "A char stores a single character such as 'A'."),
                new QuizQuestion(
                    "Data Types\n\nWhy does the size of a type matter when values are laid out in memory?",
                    new[] { "Because the next value's address depends on how many bytes the previous one used", "Because larger types are always stored before smaller ones", "Because size determines how long the variable's name may be", "Because only types of the same size may be declared together" }, 0,
                    "Locations are laid out one after another, so each type's size decides where the following value begins."),
                new QuizQuestion(
                    "Data Types\n\nWhich value best represents a bool?",
                    new[] { "true", "15.5", "'B'", "\"Mike\"" }, 0,
                    "A bool stores a logical value: true or false."),
                new QuizQuestion(
                    "Data Types\n\nWhich declaration best represents a floating-point value?",
                    new[] { "float price = 15.5;", "int age = 20;", "char grade = 'A';", "bool passed = true;" }, 0,
                    "A float stores decimal values such as 15.5."),
            } },

            { "Address Layout", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Address Layout\n\nAn int is stored at 0x1000 and an int uses 4 bytes. Where does the next variable begin?",
                    new[] { "0x1004", "0x1001", "0x1000", "0x1040" }, 0,
                    "The next location begins where the previous one ended, so the address moves forward by the size of the type."),
                new QuizQuestion(
                    "Address Layout\n\nA char is declared immediately after an int. Why does the next address move forward by only 1?",
                    new[] { "Because a char occupies 1 byte, so the following location starts 1 byte later", "Because chars are always stored last", "Because a char does not occupy memory", "Because addresses always increase by exactly 1" }, 0,
                    "The step between addresses is the size of the value just stored, not a fixed amount."),
                new QuizQuestion(
                    "Address Layout\n\nIf two variables have different addresses, what does that mean?",
                    new[] { "They occupy separate storage locations", "They must hold different values", "They must be of different types", "One of them has been removed" }, 0,
                    "Different addresses mean different locations. Their values and types are unrelated to that fact."),
                new QuizQuestion(
                    "Address Layout\n\nWhat stays the same when a variable's value is updated?",
                    new[] { "Its address", "Its value", "The result of reading it back", "The last action recorded against it" }, 0,
                    "Updating writes into the same location, so the address is unchanged while the value is not."),
                new QuizQuestion(
                    "Address Layout\n\nIn what order are consecutively declared variables normally laid out?",
                    new[] { "Each begins where the previous one ended", "In alphabetical order by name", "Largest type first, smallest last", "At addresses chosen at random" }, 0,
                    "Consecutive layout is what makes the address of the next value predictable from the size of the last."),
            } },

            { "Memory Access", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Memory Access\n\nWhat does memory access mean?",
                    new[] { "Reading or using a value stored in memory", "Reserving a new location", "Removing a stored value", "Renaming a variable" }, 0,
                    "Access is about getting at a value that is already stored."),
                new QuizQuestion(
                    "Memory Access\n\nWhen a value is read from memory, what happens to the stored value?",
                    new[] { "It stays the same - reading does not change it", "It is cleared once it has been read", "It is moved to a new address", "It is replaced by a copy of itself" }, 0,
                    "Reading takes a copy of the value. The location keeps what it held."),
                new QuizQuestion(
                    "Memory Access\n\nWhich of these is an example of memory access?",
                    new[] { "Reading age in order to display its stored value", "Declaring a new variable named age", "Removing age from memory", "Changing age's type" }, 0,
                    "Access reads what is already there. The others create, destroy or redefine a location."),
                new QuizQuestion(
                    "Memory Access\n\nIn int sum = x + y;, what must happen before the addition can be performed?",
                    new[] { "The values stored in x and y must be read from memory", "sum must already hold a value", "x and y must be removed from memory", "x and y must share a single address" }, 0,
                    "An expression reads its operands out of memory first, then computes, then stores the result."),
                new QuizQuestion(
                    "Memory Access\n\nWhy is access different from update?",
                    new[] { "Access reads a value, while update changes a stored value", "Access changes a value, while update only displays it", "Both replace the stored value", "Access reserves a location, while update names it" }, 0,
                    "Access leaves memory as it was. Update writes over what was there."),
            } },

            { "Loop Memory", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Loop Memory\n\nWhat changes during each iteration of a loop?",
                    new[] { "The element being read and the running total", "The addresses of the array elements", "The type of the accumulator", "The number of elements in the array" }, 0,
                    "Each pass reads the next element and updates the accumulator. The array itself does not move or change size."),
                new QuizQuestion(
                    "Loop Memory\n\nIn total = total + value, which variable is updated?",
                    new[] { "total", "value", "Both, equally", "Neither - a new variable is created each pass" }, 0,
                    "total is read, added to, and written back. value is only read."),
                new QuizQuestion(
                    "Loop Memory\n\nWhy is a loop counter usually removed from memory when the loop ends?",
                    new[] { "It exists only for the duration of the loop", "It is copied into the total instead", "Its address is needed by the next loop", "Counters are never stored in memory" }, 0,
                    "A counter declared for a loop belongs to that loop, and its location is released when the loop finishes."),
                new QuizQuestion(
                    "Loop Memory\n\nGiven int numbers[3] = {2, 4, 6}; and int total = 0;, what does total hold after a loop adds every element?",
                    new[] { "12", "6", "3", "0" }, 0,
                    "2 + 4 + 6 = 12. total is updated once per iteration and holds the running sum after the last one."),
                new QuizQuestion(
                    "Loop Memory\n\nWhat does the current value represent during a loop?",
                    new[] { "The element being processed in this iteration", "The total accumulated so far", "The number of iterations remaining", "The address of the array" }, 0,
                    "The current value is this pass's element, distinct from the accumulator that spans all passes."),
            } },

            { "Arrays", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Arrays\n\nHow are array elements laid out in memory?",
                    new[] { "In consecutive locations, one element after another", "At unrelated addresses chosen for each element", "All at the same address", "In reverse order of their indexes" }, 0,
                    "Consecutive layout is what lets an index be calculated rather than searched for."),
                new QuizQuestion(
                    "Arrays\n\nWhat does marks[2] = 85 do?",
                    new[] { "Replaces the value stored at index 2", "Adds a fourth element to the array", "Changes every element to 85", "Removes the element at index 2" }, 0,
                    "Only the element at the given index is written. The rest of the array is untouched."),
                new QuizQuestion(
                    "Arrays\n\nWhat is the index of the first array element in most programming languages?",
                    new[] { "0", "1", "-1", "10" }, 0,
                    "Most languages use zero-based indexing, so the first element is at index 0."),
                new QuizQuestion(
                    "Arrays\n\nIn int marks[3] = {70, 80, 65};, what is marks[1]?",
                    new[] { "80", "70", "65", "3" }, 0,
                    "Index 0 holds 70, index 1 holds 80 and index 2 holds 65."),
                new QuizQuestion(
                    "Arrays\n\nAn int array begins at 0x1000 and an int uses 4 bytes. What is the address of element 1?",
                    new[] { "0x1004", "0x1001", "0x1000", "0x1008" }, 0,
                    "Element 0 occupies 0x1000 to 0x1003, so element 1 begins at 0x1004."),
            } },

            { "Functions", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Functions\n\nWhat is created when a function is called?",
                    new[] { "A temporary frame holding its parameters and local variables", "A permanent copy of the whole program", "A new array in global memory", "A second copy of the caller's variables" }, 0,
                    "A function call creates temporary memory for its parameters and local variables."),
                new QuizQuestion(
                    "Functions\n\nWhat happens to a function's local variables after it finishes?",
                    new[] { "They are removed along with the function's frame", "They become global variables", "They keep their values ready for the next call", "They are copied into the return value" }, 0,
                    "Local variables exist only while the function is running."),
                new QuizQuestion(
                    "Functions\n\nWhat are parameters?",
                    new[] { "Input values a function receives when it is called", "Values a function returns to its caller", "Variables that survive after the function ends", "The memory addresses the function occupies" }, 0,
                    "Parameters receive values when a function is called, and live in the call's frame."),
                new QuizQuestion(
                    "Functions\n\nWhat does return do in a function?",
                    new[] { "Sends a result back to the calling statement", "Removes the returned variable from memory permanently", "Restarts the function from its first line", "Converts a local variable into a global one" }, 0,
                    "return sends a computed value back from the function to whatever called it."),
                new QuizQuestion(
                    "Functions\n\nWhy is a function's memory temporary?",
                    new[] { "The frame exists only while the function is executing", "Functions are not allowed to store values", "The frame is shared with the caller and overwritten", "Only global variables occupy memory" }, 0,
                    "Frames are created on a call and removed when the function completes."),
            } },

            { "Selection", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Selection\n\nWhat does an if condition decide?",
                    new[] { "Which branch of code runs", "Which variables are allocated", "The value stored in the variable being tested", "The order in which variables are declared" }, 0,
                    "An if condition determines whether the true branch or the alternative runs."),
                new QuizQuestion(
                    "Selection\n\nHow can a selection statement affect memory?",
                    new[] { "Only the branch that runs can update memory", "Every branch updates memory regardless of the condition", "Selection never changes stored values", "Both branches run and the second overwrites the first" }, 0,
                    "Only the executed branch writes to memory. The skipped branch has no effect at all."),
                new QuizQuestion(
                    "Selection\n\nIf score >= 50 is true, which branch runs?",
                    new[] { "The if branch", "The else branch", "Both branches, in order", "Neither branch" }, 0,
                    "When the condition is true the if branch runs and the else branch is skipped."),
                new QuizQuestion(
                    "Selection\n\nIf the condition of an if/else statement is false, what happens?",
                    new[] { "The else branch runs", "The if branch runs anyway", "Both branches are skipped", "The condition is evaluated a second time" }, 0,
                    "The else branch handles the false case, so exactly one of the two always runs."),
                new QuizQuestion(
                    "Selection\n\nscore is 45 and the code is: if (score >= 50) result = 1; else result = 0; What is stored in result?",
                    new[] { "0", "1", "45", "Nothing - result is never assigned" }, 0,
                    "45 >= 50 is false, so the else branch runs and result becomes 0. Only the branch that executes writes to memory."),
            } },

            { "Scope", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Scope\n\nWhat does scope describe?",
                    new[] { "The region of a program in which a variable can be accessed", "The amount of memory a variable uses", "The order in which variables are declared", "The type of value a variable holds" }, 0,
                    "Scope is about WHERE a name can be reached."),
                new QuizQuestion(
                    "Scope\n\nA variable declared inside an if block is accessible where?",
                    new[] { "Only inside that block", "Anywhere in the program", "Only after the block ends", "Only inside functions called from that block" }, 0,
                    "A variable declared inside a block belongs to that block and is removed when the block ends."),
                new QuizQuestion(
                    "Scope\n\nWhat is a global variable?",
                    new[] { "One accessible from a wider region of the program", "One that can never be updated", "One stored without an address", "One that exists only inside a loop" }, 0,
                    "A global variable is reachable from a broader part of the program than a local one."),
                new QuizQuestion(
                    "Scope\n\nWhy do scope errors happen?",
                    new[] { "A variable is used outside the region where it exists", "Two variables are given the same value", "A variable is read more than once", "A type uses more bytes than expected" }, 0,
                    "Using a name where it is not in scope means there is no location for it to refer to."),
                new QuizQuestion(
                    "Scope\n\nint x = 10; is declared outside an if block and int y = 20; inside it. After the block ends, what remains in memory?",
                    new[] { "x only", "y only", "Both x and y", "Neither" }, 0,
                    "y belonged to the block and is removed when it ends. x was declared outside, so it survives."),
            } },

            { "Remove Variable", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Remove Variable\n\nWhat does removing a variable from memory mean?",
                    new[] { "Its storage location is no longer available", "Its value is set to zero but the location remains", "It keeps its value but can no longer be updated", "It is converted into a global variable" }, 0,
                    "Removal means the record is gone, not that it holds a different value."),
                new QuizQuestion(
                    "Remove Variable\n\nWhat happens if a program tries to read a variable that has been removed?",
                    new[] { "There is no location to read, so the access fails", "The last value it held is returned", "A new variable with that name is created", "The nearest variable in memory is read instead" }, 0,
                    "A removed name has no location behind it, so there is nothing to read."),
                new QuizQuestion(
                    "Remove Variable\n\nWhy might a variable be removed from memory?",
                    new[] { "Its lifetime or scope has ended", "Its value grew too large for its type", "It was read too many times", "It was declared before any other variable" }, 0,
                    "Variables are released when they are no longer needed or no longer in scope."),
                new QuizQuestion(
                    "Remove Variable\n\nage and price are both in memory, and age is removed. What is true of price?",
                    new[] { "It is unaffected and still holds its value", "It is removed as well", "Its value is reset to zero", "It takes over age's address" }, 0,
                    "Removing one storage location does not disturb any other."),
                new QuizQuestion(
                    "Remove Variable\n\nWhat does the lifetime of a variable mean?",
                    new[] { "The period during which its storage location exists", "The number of times it can be read", "The number of bytes it occupies", "The region of code that can see it" }, 0,
                    "Lifetime is about WHEN a location exists. Scope is about WHERE it can be reached. The two are related but not the same."),
            } },

            { "Trace Exercise", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Trace Exercise\n\nWhat does tracing a program mean?",
                    new[] { "Following it statement by statement and predicting the effect of each", "Running it and reading only the final output", "Rewriting it in a different language", "Removing statements until it works" }, 0,
                    "Tracing means following each instruction and predicting what it does to memory."),
                new QuizQuestion(
                    "Trace Exercise\n\nint x = 4; int y = 6; x = y + 2; What does x hold at the end?",
                    new[] { "8", "6", "4", "10" }, 0,
                    "y holds 6, so y + 2 is 8, and that replaces x's previous value of 4."),
                new QuizQuestion(
                    "Trace Exercise\n\nint p = 5; int q = p; p = 9; What does q hold at the end?",
                    new[] { "5", "9", "14", "Nothing - q follows p" }, 0,
                    "q received a COPY of p's value at the moment of assignment. Changing p afterwards does not reach q."),
                new QuizQuestion(
                    "Trace Exercise\n\nint a = 10; int b = 3; b = a - b; What does b hold at the end?",
                    new[] { "7", "13", "3", "10" }, 0,
                    "The right-hand side is evaluated first using b's OLD value: 10 - 3 = 7. Only then is the result stored back into b."),
                new QuizQuestion(
                    "Trace Exercise\n\nIn a = b + c;, which values are read and which is written?",
                    new[] { "b and c are read; a is written", "a and b are read; c is written", "All three are read and written", "Only a is read; b and c are written" }, 0,
                    "An assignment reads everything on the right, then writes the single location on the left."),
            } },

            { "Debug Exercise", new List<QuizQuestion>
            {
                new QuizQuestion(
                    "Debug Exercise\n\nWhat does debugging mean?",
                    new[] { "Finding and correcting errors in code or logic", "Writing code without testing it", "Removing features to make a program shorter", "Translating code into another language" }, 0,
                    "Debugging means identifying and fixing errors."),
                new QuizQuestion(
                    "Debug Exercise\n\nint age = 20; then age = \"Mike\"; What is wrong with the second line?",
                    new[] { "age is an int, so it cannot hold a string", "A variable can only be assigned once", "\"Mike\" is missing a semicolon", "age must be removed before it is reassigned" }, 0,
                    "The declared type constrains what the location may hold. An int location cannot store a string, so the assignment is rejected and 20 remains."),
                new QuizQuestion(
                    "Debug Exercise\n\nint marks[3] = {70, 80, 90}; then marks[3] = 100; What is wrong?",
                    new[] { "Valid indexes are 0, 1 and 2, so index 3 is outside the array", "Arrays cannot be updated after declaration", "The array is too small to hold three values", "100 is too large to store in an int" }, 0,
                    "For size 3 the valid indexes are 0, 1 and 2. marks[3] refers to a location the array does not own."),
                new QuizQuestion(
                    "Debug Exercise\n\nIf a variable ends up holding the wrong value, what should be examined first?",
                    new[] { "The statement that last wrote to it", "The variable's address", "The number of bytes its type uses", "The order the variables were declared in" }, 0,
                    "A wrong value came from a write. Finding the statement responsible is what locates the fault."),
                new QuizQuestion(
                    "Debug Exercise\n\nchar grade = 'A'; then grade = 'AB'; What is wrong with the second line?",
                    new[] { "A char holds exactly one character, and 'AB' is two", "Single quotes may not be used twice in one program", "grade should have been declared as an int", "A char cannot be reassigned once set" }, 0,
                    "A char location holds a single character, so a two-character literal does not fit it."),
            } },

            // Godot: "Final Assessment": [] — deliberately empty, and
            // MemoryController hides the Quiz button in that activity. The two
            // facts agree, so this is a decision rather than an oversight.
            { "Final Assessment", new List<QuizQuestion>() }
        };

        /// <summary>
        /// Data-selection half of load_quiz_for_current_module()'s if/elif
        /// chain — the title-label / randomize / shuffle / empty-check half
        /// stays in QuizController, matching the Module-vs-Controller split
        /// used by every other Phase (StackModule holds data, StackController
        /// drives UI). moduleKey must match ResearchLogger.ModuleName /
        /// AppState.CurrentModule / ChangeModuleController.ModuleGroupEntry's
        /// convention exactly ("ArrayList", "Stack", "Queue", "LinkedList",
        /// "Functions", "Loops", "Selection", "Memory").
        ///
        /// Returns a freshly cloned list every call (never the static bank
        /// itself) so a caller is always free to shuffle/reorder the result
        /// without risk of mutating shared state — see QuizQuestion.Clone().
        /// Memory returns an empty list (deferred — see class comment);
        /// an unrecognized key also returns an empty list, matching source's
        /// quiz_questions staying empty when current_module matches none of
        /// the if/elif branches.
        ///
        /// selectionMode is only consulted for moduleKey "Selection", and is
        /// the deviation described in deviation (a) of the Selection bank
        /// comment above: pass SelectionController.CurrentMode ("Basic If",
        /// "Else-if Ladder" or "Nested If") and only that mode's questions
        /// come back, with their "Mode: X" prefix already stripped. Pass null
        /// or empty and the whole mixed bank comes back with prefixes intact,
        /// which is source's own behaviour — that path is the fallback when
        /// no SelectionController can be located, so a missing reference
        /// degrades to the original quiz rather than to no quiz at all.
        /// </summary>
        public static List<QuizQuestion> GetQuestionsForModule(string moduleKey, string subMode = null)
        {
            // Memory is keyed by activity, so it does not go through the
            // switch below at all — its bank IS a per-sub-mode dictionary.
            if (moduleKey == "Memory")
            {
                return GetMemoryQuestions(subMode);
            }

            List<QuizQuestion> source;

            switch (moduleKey)
            {
                case "ArrayList":
                    source = ArrayQuizQuestions;
                    break;
                case "Stack":
                    source = StackQuizQuestions;
                    break;
                case "Queue":
                    source = QueueQuizQuestions;
                    break;
                case "LinkedList":
                    source = LinkedListQuizQuestions;
                    break;
                case "Functions":
                    source = FunctionsQuizQuestions;
                    break;
                case "Loops":
                    source = LoopQuizQuestions;
                    break;
                case "Selection":
                    source = SelectionQuizQuestions;
                    break;
                default:
                    // Any unrecognized key. "Memory" is handled above.
                    return new List<QuizQuestion>();
            }

            List<QuizQuestion> cloned = new List<QuizQuestion>(source.Count);
            foreach (QuizQuestion q in source)
            {
                cloned.Add(q.Clone());
            }

            if (moduleKey == "Selection" && !string.IsNullOrEmpty(subMode))
            {
                cloned = FilterSelectionByMode(cloned, subMode);
            }

            return cloned;
        }

        /// <summary>
        /// Deviation (a). Keeps only the questions whose text opens with
        /// "Mode: {selectionMode}\n\n" and strips that prefix, mirroring what
        /// source already does for Memory via
        /// get_memory_activity_quiz_questions() + clean_quiz_question_text().
        ///
        /// If the mode string matches nothing — a typo, or a mode added to
        /// the controller without questions being written for it — the
        /// UNFILTERED list is returned rather than an empty one. An empty
        /// list would trip QuizController's "No quiz questions are available
        /// for this module" path and make the Quiz button look broken; the
        /// mixed bank is a much better failure mode, and it is what source
        /// shows anyway.
        ///
        /// Operates on the already-cloned list, so the static bank keeps its
        /// prefixes for the next call.
        /// </summary>
        private static List<QuizQuestion> FilterSelectionByMode(List<QuizQuestion> questions, string selectionMode)
        {
            string prefix = "Mode: " + selectionMode + "\n\n";

            List<QuizQuestion> matched = new List<QuizQuestion>(questions.Count);
            foreach (QuizQuestion q in questions)
            {
                if (q.Question != null && q.Question.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    q.Question = q.Question.Substring(prefix.Length).Trim();
                    matched.Add(q);
                }
            }

            return matched.Count > 0 ? matched : questions;
        }

        /// <summary>
        /// Godot: get_memory_activity_quiz_questions(activity_name) —
        ///
        ///     var selected_questions: Array = questions_by_activity.get(activity_name, [])
        ///     return selected_questions.duplicate(true)
        ///
        /// An unknown activity returns an empty list, exactly as source's
        /// dictionary lookup default does, and so does a null/empty activity
        /// — there is no sensible "all activities" fallback here the way
        /// there is for Selection, because Memory's questions were always
        /// per-activity.
        ///
        /// The prefix is stripped here rather than at display time. Source
        /// strips it in clean_quiz_question_text() instead, which is a
        /// display-time concern, but the effect is identical and doing it
        /// once at load avoids re-running it for every question shown.
        /// </summary>
        private static List<QuizQuestion> GetMemoryQuestions(string activityName)
        {
            if (string.IsNullOrEmpty(activityName)) return new List<QuizQuestion>();

            if (!MemoryQuizQuestions.TryGetValue(activityName, out List<QuizQuestion> source))
            {
                return new List<QuizQuestion>();
            }

            List<QuizQuestion> cloned = new List<QuizQuestion>(source.Count);

            foreach (QuizQuestion q in source)
            {
                QuizQuestion copy = q.Clone();
                copy.Question = CleanMemoryQuestion(copy.Question, activityName);
                cloned.Add(copy);
            }

            return cloned;
        }

        /// <summary>
        /// Godot: clean_quiz_question_text()'s Memory branch, which strips
        /// TWO prefixes:
        ///
        ///     var activity_prefix: String = current_memory_activity + "\n\n"
        ///     ...
        ///     # Special case for Overview questions.
        ///     var overview_prefix: String = "Memory Overview\n\n"
        ///
        /// The special case exists because Overview's questions are prefixed
        /// "Memory Overview" while the activity is named "Overview" — the one
        /// place in the bank where the two disagree. Both are handled.
        /// </summary>
        private static string CleanMemoryQuestion(string questionText, string activityName)
        {
            string cleaned = (questionText ?? "").Trim();

            string activityPrefix = activityName + "\n\n";
            if (cleaned.StartsWith(activityPrefix, System.StringComparison.Ordinal))
            {
                cleaned = cleaned.Substring(activityPrefix.Length).Trim();
            }

            const string OverviewPrefix = "Memory Overview\n\n";
            if (cleaned.StartsWith(OverviewPrefix, System.StringComparison.Ordinal))
            {
                cleaned = cleaned.Substring(OverviewPrefix.Length).Trim();
            }

            return cleaned;
        }

        /// <summary>
        /// Direct port of get_current_module_display_name(). Used both for
        /// the quiz title ("{name} Mini Quiz") and the results title
        /// ("{name} Quiz Completed") — except Memory, which source special-
        /// cases in both places to use current_memory_activity instead (see
        /// class comment; not reachable today since Memory is deferred).
        /// </summary>
        /// Godot titles a Memory quiz with the ACTIVITY name, not the module
        /// name — load_quiz_for_current_module() sets
        /// `current_memory_activity + " Mini Quiz"` where every other module
        /// uses its own display name. Pass the activity as subMode to get
        /// that; omit it and Memory falls back to "Memory Management".
        public static string GetDisplayName(string moduleKey, string subMode = null)
        {
            if (moduleKey == "Memory" && !string.IsNullOrEmpty(subMode)) return subMode;

            switch (moduleKey)
            {
                case "ArrayList": return "Array/List";
                case "Stack": return "Stack";
                case "Queue": return "Queue";
                case "LinkedList": return "Linked List";
                case "Functions": return "Functions";
                case "Loops": return "Loops";
                case "Selection": return "Selection";
                case "Memory": return "Memory Management";
                default: return "Current Module";
            }
        }
    }
}
