namespace MartProg.Modules
{
    /// <summary>
    /// Phase 20: implemented by every teaching-module controller
    /// (ArrayListController, StackController, QueueController, and any future
    /// one) so ChangeModuleController.cs can reset + refresh whichever module
    /// the student switches to, without a per-module if/else chain living
    /// inside the module-switcher itself.
    ///
    /// This is a deliberate C# adaptation, not a literal method-for-method
    /// port. Godot's ar_learning_screen.gd reaches the same goal with duck
    /// typing — reset_dashboard_state_for_module_start() and
    /// show_selected_module() both call things like
    /// `if array_module.has_method("reset_array"): array_module.reset_array()`
    /// against whichever module happens to be wired. C# MonoBehaviours don't
    /// have has_method()/call() built in the same way; an explicit interface
    /// gets the same "call this module's own entry hook, generically" result
    /// with compile-time safety instead of stringly-typed lookups.
    /// </summary>
    public interface IModuleController
    {
        /// <summary>
        /// Resets this module's data to its starting values and refreshes
        /// every dashboard panel (Summary / Instruction / Operation /
        /// Explanation / Status / Progress) to match.
        ///
        /// Matches Godot's show_selected_module(): it calls
        /// reset_dashboard_state_for_module_start() (which resets the
        /// module's *data*, e.g. stack_dashboard_values = [10, 20, 30], and
        /// calls stack_module.reset_stack()) plus update_stack_dashboard()
        /// (which refreshes the title/instruction/operation/explanation text)
        /// EVERY time that module is displayed — including a return visit to
        /// a module the student already used earlier in the session, not
        /// just the very first time. EnterModule() is this controller's
        /// single entry point for that "always reset on display" behavior,
        /// called both by this controller's own Start() (first activation)
        /// and by ChangeModuleController (every later switch) — see Migration
        /// Spec §8f for the full reasoning.
        /// </summary>
        void EnterModule();
    }
}
