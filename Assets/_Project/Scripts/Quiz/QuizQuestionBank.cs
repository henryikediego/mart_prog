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
                "What does numbers.pop() do in this module?",
                new[] { "Removes the first value", "Removes the last value", "Adds a new value", "Sorts the list" }, 1,
                "In this module, pop() removes the last value in the list."),
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
                "Which Python command removes the front value in this queue module?",
                new[] { "queue.append(value)", "queue.pop()", "queue.pop(0)", "queue[-1]" }, 2,
                "queue.pop(0) removes the first value in the list, which represents the front of the queue."),
        };

        // -----------------------------------------------------------------
        // Linked List — linked_list_quiz_questions (ar_learning_screen.gd)
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> LinkedListQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                "What is a linked list made of?",
                new[] { "Indexes", "Nodes", "Stacks", "Queues" }, 1,
                "A linked list is made of nodes. Each node usually stores data and a link to the next node."),
            new QuizQuestion(
                "What is the first node in a linked list called?",
                new[] { "Tail", "Rear", "Head", "Top" }, 2,
                "The first node in a linked list is called the head."),
            new QuizQuestion(
                "What is the last node in a linked list called?",
                new[] { "Head", "Tail", "Front", "Index" }, 1,
                "The last node in a linked list is called the tail."),
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
                "What happens when a new node is inserted at the tail?",
                new[] { "It becomes the new head", "It is added after the current tail", "It removes the head", "It deletes the list" }, 1,
                "Inserting at the tail adds the new node after the current tail node."),
            new QuizQuestion(
                "What happens when the head node is deleted?",
                new[] { "The tail becomes null", "The head moves to the next node", "All nodes are deleted", "The list becomes a stack" }, 1,
                "Deleting the head means the head reference moves to the next node."),
            new QuizQuestion(
                "How is a linked list usually traversed?",
                new[] { "By jumping directly to any index", "From head to tail by following links", "From rear to front", "Only from the middle" }, 1,
                "A linked list is traversed from the head node to the tail node by following each next link."),
        };

        // -----------------------------------------------------------------
        // Functions — functions_quiz_questions (ar_learning_screen.gd)
        // Not yet reachable — Functions module isn't built in Unity yet.
        // -----------------------------------------------------------------
        public static readonly List<QuizQuestion> FunctionsQuizQuestions = new List<QuizQuestion>
        {
            new QuizQuestion(
                "What is a function in programming?",
                new[] { "A reusable block of code", "A type of queue", "A memory address", "A loop condition" }, 0,
                "A function is a reusable block of code designed to perform a specific task."),
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
                "Why are functions useful?",
                new[] { "They make code reusable and organized", "They remove all variables", "They stop programs from running", "They only work with arrays" }, 0,
                "Functions help organize code and allow the same logic to be reused many times."),
            new QuizQuestion(
                "What happens first when add(10, 20) is called?",
                new[] { "The function receives the input values", "The program deletes the function", "The queue is searched", "The result is ignored" }, 0,
                "When the function is called, the input values 10 and 20 are passed into the parameters a and b."),
            new QuizQuestion(
                "Which statement correctly represents a simple function that adds two values?",
                new[] { "def add(a, b): return a + b", "queue.pop(0)", "stack.append(value)", "numbers.index(value)" }, 0,
                "def add(a, b): return a + b defines a function that returns the sum of two values."),
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
                "What does one Step do in the Loop module?",
                new[] { "Runs one loop iteration", "Deletes all values", "Resets the module", "Changes to another module" }, 0,
                "Step runs one iteration by visiting the next value and updating the running total."),
            new QuizQuestion(
                "What does Run Loop do?",
                new[] { "Visits all values and calculates the final total", "Only visits the first value", "Removes the last value", "Stops the quiz" }, 0,
                "Run Loop visits all values from left to right and updates the total until the loop finishes."),
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
        public static List<QuizQuestion> GetQuestionsForModule(string moduleKey, string selectionMode = null)
        {
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
                    // Includes "Memory" (deferred) and any unrecognized key.
                    return new List<QuizQuestion>();
            }

            List<QuizQuestion> cloned = new List<QuizQuestion>(source.Count);
            foreach (QuizQuestion q in source)
            {
                cloned.Add(q.Clone());
            }

            if (moduleKey == "Selection" && !string.IsNullOrEmpty(selectionMode))
            {
                cloned = FilterSelectionByMode(cloned, selectionMode);
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
        /// Direct port of get_current_module_display_name(). Used both for
        /// the quiz title ("{name} Mini Quiz") and the results title
        /// ("{name} Quiz Completed") — except Memory, which source special-
        /// cases in both places to use current_memory_activity instead (see
        /// class comment; not reachable today since Memory is deferred).
        /// </summary>
        public static string GetDisplayName(string moduleKey)
        {
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
