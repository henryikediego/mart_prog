using System;

namespace MartProg.Quiz
{
    /// <summary>
    /// Phase 22: plain data record for one quiz question. Ported from the
    /// Dictionary shape used throughout ar_learning_screen.gd's question-bank
    /// arrays (array_quiz_questions, stack_quiz_questions, queue_quiz_questions,
    /// linked_list_quiz_questions, functions_quiz_questions, loop_quiz_questions,
    /// get_selection_quiz_questions()) — every entry there has exactly these
    /// four keys: "question", "options" (an array of exactly 4 strings),
    /// "answer" (an int index 0-3 into options), and "explanation". A typed
    /// class replaces the untyped Dictionary here; the shape and the data
    /// itself are unchanged from source.
    /// </summary>
    [Serializable]
    public class QuizQuestion
    {
        public string Question;
        public string[] Options;
        public int Answer;
        public string Explanation;

        public QuizQuestion(string question, string[] options, int answer, string explanation)
        {
            Question = question;
            Options = options;
            Answer = answer;
            Explanation = explanation;
        }

        /// <summary>
        /// Deep copy — matches Godot's question_item.duplicate(true) calls in
        /// randomize_quiz_options() / load_quiz_for_current_module(), which
        /// always work on a copy so the static question-bank arrays
        /// themselves are never mutated by shuffling. QuizQuestionBank's
        /// static lists are the Unity equivalent of those source arrays and
        /// must stay just as untouched — every question handed to
        /// QuizController goes through this Clone() first.
        /// </summary>
        public QuizQuestion Clone()
        {
            return new QuizQuestion(Question, (string[])Options.Clone(), Answer, Explanation);
        }
    }
}
