using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProductionFlow
{
    public class RecipeNode
    {
        public RecipeDef Recipe { get; set; }
        public List<RecipeNode> Children { get; set; }
        
        public RecipeNode(RecipeDef recipe)
        {
            Recipe = recipe;
            Children = new List<RecipeNode>();
        }
    }
    
    public class MainTabWindow_ProductionFlow : MainTabWindow
    {
        // Cached icon for search workbench button
        private static Texture2D searchIcon = null;
        
        private static Texture2D SearchIcon
        {
            get
            {
                if (searchIcon == null)
                {
                    // Try to get search icon (magnifying glass) from RimWorld
                    // Standard RimWorld search icon paths
                    searchIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Search", false);
                    if (searchIcon == null)
                    {
                        searchIcon = ContentFinder<Texture2D>.Get("UI/Overlays/Search", false);
                    }
                    if (searchIcon == null)
                    {
                        searchIcon = ContentFinder<Texture2D>.Get("UI/Commands/ViewQuest", false);
                    }
                    if (searchIcon == null)
                    {
                        // Last fallback - try to use TexButton if available
                        try
                        {
                            var texButtonType = typeof(TexButton);
                            var searchField = texButtonType.GetField("Search", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (searchField != null)
                            {
                                searchIcon = searchField.GetValue(null) as Texture2D;
                            }
                        }
                        catch { }
                    }
                }
                return searchIcon;
            }
        }
        // Scroll positions for each panel
        private Vector2 recipesScrollPosition = Vector2.zero;
        private Vector2 workbenchesScrollPosition = Vector2.zero;
        private Vector2 pawnsScrollPosition = Vector2.zero;
        private Vector2 recipeInfoScrollPosition = Vector2.zero;
        private Vector2 workbenchBillsScrollPosition = Vector2.zero;
        private Vector2 materialsScrollPosition = Vector2.zero;
        private Vector2 relatedRecipesScrollPosition = Vector2.zero;
        
        // Selected recipe
        private RecipeDef selectedRecipe = null;
        private RecipeDef lastSelectedRecipe = null;
        
        // Selected workbenches and pawns
        private HashSet<Thing> selectedWorkbenches = new HashSet<Thing>();
        private HashSet<Pawn> selectedPawns = new HashSet<Pawn>();
        
        // Hovered workbench (for showing bills)
        private Thing hoveredWorkbench = null;
        
        // Selected workbench for viewing/managing bills
        private Thing selectedWorkbenchForBills = null;
        
        // Mouseover bill for showing ingredient radius
        private Bill mouseoverBill = null;
        
        // ThingFilter for materials selection
        private ThingFilter materialFilter = null;
        private ThingFilterUI.UIState materialFilterUIState = new ThingFilterUI.UIState();
        private RecipeDef materialFilterRecipe = null; // Track which recipe the filter is for
        
        // Scroll view heights
        private float recipesScrollHeight = 0f;
        private float workbenchesScrollHeight = 0f;
        private float pawnsScrollHeight = 0f;
        private float recipeInfoScrollHeight = 0f;
        private float workbenchBillsScrollHeight = 0f;
        private float relatedRecipesScrollHeight = 0f;
        
        // Bill creation settings
        private int quantity = 1;
        private int targetCount = 10;
        private QualityCategory? selectedQuality = null;
        private string quantityBuffer = "1";
        private string targetCountBuffer = "10";
        private BillRepeatModeDef selectedRepeatMode = BillRepeatModeDefOf.RepeatCount;
        
        // Recipe search
        private string recipeSearchText = "";
        
        // Related recipes hierarchy
        private HashSet<RecipeDef> expandedRelatedRecipes = new HashSet<RecipeDef>();
        private const int MAX_RECIPE_HIERARCHY_DEPTH = 2; // Maximum depth for recipe hierarchy (reduced for performance)
        
        // Expanded settlements for workbench grouping
        private HashSet<Map> expandedSettlements = new HashSet<Map>();
        private Dictionary<string, List<RecipeDef>> recipeCacheByFilterSummary = new Dictionary<string, List<RecipeDef>>();
        private RecipeDef cachedRecipeForFilter = null;
        private List<RecipeDef> cachedAllRecipes = null;
        private int cachedAllRecipesFrame = -1;
        

        public override Vector2 InitialSize
        {
            get 
            { 
                float maxHeight = Screen.height * 0.8f;
                return new Vector2(1600f, Mathf.Min(800f, maxHeight)); 
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Reset hovered workbench at start of frame
            hoveredWorkbench = null;
            
            // Reset mouseover bill at start of frame
            mouseoverBill = null;
            
            // Divide window into 4 columns (each 1/4 width)
            float margin = 5f;
            float columnWidth = (inRect.width - margin * 5) / 4f;
            
            // Calculate row height based on font size + 10%
            float rowHeight = Text.CalcSize("A").y * 1.1f;
            float minRowHeight = 20f;
            rowHeight = Mathf.Max(rowHeight, minRowHeight);
            
            // Calculate input field height
            float inputFieldHeight = 30f;
            
            // Calculate controls height (buttons for bill creation)
            float controlHeight = 30f;
            // Quality (if applicable) + Repeat mode + Quantity/Target (if applicable) + Create button
            float controlsTotalHeight = controlHeight; // Create button (always shown, at bottom)
            if (selectedRecipe != null && selectedRecipe.workSkill != null)
            {
                controlsTotalHeight += controlHeight + 5f; // Quality
            }
            controlsTotalHeight += controlHeight + 5f; // Repeat mode
            if (selectedRepeatMode == BillRepeatModeDefOf.RepeatCount || selectedRepeatMode == BillRepeatModeDefOf.TargetCount)
            {
                controlsTotalHeight += controlHeight + 5f; // Quantity/Target
            }
            
            // Column 1: Recipe search (top) + Recipes (below search) + Recipe info (middle) + Controls (bottom)
            float recipeSearchHeight = inputFieldHeight + margin;
            Rect recipeSearchRect = new Rect(inRect.x, inRect.y, columnWidth, inputFieldHeight);
            DrawRecipeSearch(recipeSearchRect);
            
            float recipesTop = inRect.y + recipeSearchHeight;
            float recipesHeight = 250f; // Fixed height, independent from other panels
            Rect recipesRect = new Rect(inRect.x, recipesTop, columnWidth, recipesHeight);
            DrawRecipesPanel(recipesRect, rowHeight);
            
            float recipeInfoTop = recipesTop + recipesHeight + margin;
            float recipeInfoBottom = inRect.y + inRect.height - controlsTotalHeight - 10f;
            float recipeInfoHeight = recipeInfoBottom - recipeInfoTop;
            Rect recipeInfoRect = new Rect(inRect.x, recipeInfoTop, columnWidth, recipeInfoHeight);
            DrawRecipeInfoPanel(recipeInfoRect);
            
            // Column 2: Workbenches (top) + Materials (bottom) - independent sizes
            float workbenchesHeight = 250f; // Fixed height
            Rect workbenchesRect = new Rect(inRect.x + columnWidth + margin, inRect.y, columnWidth, workbenchesHeight);
            DrawWorkbenchesPanel(workbenchesRect, rowHeight);
            
            float materialsTop = inRect.y + workbenchesHeight + margin;
            float materialsHeight = inRect.height - workbenchesHeight - margin; // Remaining height
            Rect materialsRect = new Rect(inRect.x + columnWidth + margin, materialsTop, columnWidth, materialsHeight);
            DrawMaterialsPanel(materialsRect, rowHeight);
            
            // Column 3: Workbench bills (full height)
            Rect workbenchBillsRect = new Rect(inRect.x + (columnWidth + margin) * 2, inRect.y, columnWidth, inRect.height);
            DrawWorkbenchBillsPanel(workbenchBillsRect, rowHeight);
            
            // Column 4: Pawns (half height) + Related Recipes (half height below)
            float pawnsHeight = inRect.height / 2f - margin / 2f;
            Rect pawnsRect = new Rect(inRect.x + (columnWidth + margin) * 3, inRect.y, columnWidth, pawnsHeight);
            DrawPawnsPanel(pawnsRect, rowHeight);
            
            float relatedRecipesTop = inRect.y + pawnsHeight + margin;
            float relatedRecipesHeight = inRect.height - pawnsHeight - margin;
            Rect relatedRecipesRect = new Rect(inRect.x + (columnWidth + margin) * 3, relatedRecipesTop, columnWidth, relatedRecipesHeight);
            DrawRelatedRecipesPanel(relatedRecipesRect, rowHeight);
            
            // Bill creation controls (bottom left, below recipe info)
            Rect controlsRect = new Rect(inRect.x, inRect.y + inRect.height - controlsTotalHeight, columnWidth, controlsTotalHeight);
            DrawBillCreationControls(controlsRect, controlHeight);
            
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            
            // Draw ingredient search radius for mouseover bill (like ITab_Bills.TabUpdate)
            if (mouseoverBill != null)
            {
                Thing workbenchToShow = selectedWorkbenchForBills != null ? selectedWorkbenchForBills : hoveredWorkbench;
                if (workbenchToShow != null && workbenchToShow is Thing workbenchThing)
                {
                    mouseoverBill.TryDrawIngredientSearchRadiusOnMap(workbenchThing.Position);
                }
                mouseoverBill = null;
            }
        }

        private void DrawRecipeSearch(Rect rect)
        {
            recipeSearchText = Widgets.TextField(rect, recipeSearchText);
        }
        
        private void DrawRecipesPanel(Rect rect, float rowHeight)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            Widgets.Label(headerRect, "ProductionFlow.Recipes".Translate());
            
            Rect scrollRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, recipesScrollHeight);
            
            Widgets.BeginScrollView(scrollRect, ref recipesScrollPosition, viewRect);
            
            List<RecipeDef> allRecipes = GetAllRecipes();
            
            // Filter recipes by search text
            IEnumerable<RecipeDef> filteredRecipes = allRecipes;
            if (!string.IsNullOrEmpty(recipeSearchText))
            {
                string searchLower = recipeSearchText.ToLower();
                filteredRecipes = allRecipes.Where(recipe =>
                {
                    string recipeLabel = recipe.LabelCap.ToString().ToLower();
                    if (recipe.products != null && recipe.products.Count > 0)
                    {
                        var firstProduct = recipe.products[0];
                        if (firstProduct.thingDef != null)
                        {
                            string productLabel = firstProduct.thingDef.LabelCap.ToString().ToLower();
                            if (productLabel.Contains(searchLower) || recipeLabel.Contains(searchLower))
                            {
                                return true;
                            }
                        }
                    }
                    return recipeLabel.Contains(searchLower);
                });
            }
            
            List<RecipeDef> recipeList = filteredRecipes.OrderBy(r => r.label).ToList();
            
            if (recipeList.Count == 0)
            {
                Rect noRecipesRect = new Rect(0f, 0f, viewRect.width, rowHeight);
                Widgets.Label(noRecipesRect, "ProductionFlow.NoRecipes".Translate());
                recipesScrollHeight = rowHeight;
            }
            else
            {
                float yPos = 0f;
                float recipeRowHeight = rowHeight + 2f;
                foreach (RecipeDef recipe in recipeList)
                {
                    Rect recipeRect = new Rect(0f, yPos, viewRect.width, rowHeight);
                    
                    if (yPos - recipesScrollPosition.y + rowHeight >= 0f && yPos - recipesScrollPosition.y <= scrollRect.height)
                    {
                        DrawRecipeRow(recipeRect, recipe);
                    }
                    
                    yPos += recipeRowHeight;
                }
                recipesScrollHeight = yPos;
            }
            
            Widgets.EndScrollView();
        }

        private void DrawRecipeRow(Rect rect, RecipeDef recipe)
        {
            bool isSelected = selectedRecipe == recipe;
            
            if (isSelected)
            {
                Widgets.DrawHighlight(rect);
            }
            
            if (Widgets.ButtonInvisible(rect))
            {
                selectedRecipe = recipe;
                selectedWorkbenches.Clear();
                selectedPawns.Clear();
                
                // Reset material filter when recipe changes
                if (selectedRecipe != lastSelectedRecipe)
                {
                    materialFilter = null;
                    materialFilterRecipe = null;
                    lastSelectedRecipe = selectedRecipe;
                    // Clear recipe cache when recipe changes
                    recipeCacheByFilterSummary.Clear();
                    cachedRecipeForFilter = selectedRecipe;
                }
            }
            
            // Draw recipe icon
            Texture2D icon = null;
            if (recipe.products != null && recipe.products.Count > 0)
            {
                var firstProduct = recipe.products[0];
                if (firstProduct.thingDef != null)
                {
                    icon = firstProduct.thingDef.uiIcon;
                }
            }
            
            float iconSize = rect.height * 0.8f;
            float iconOffset = 5f;
            
            if (icon != null)
            {
                Rect iconRect = new Rect(rect.x + iconOffset, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon);
                iconOffset += iconSize + 5f;
            }
            
            Rect labelRect = new Rect(rect.x + iconOffset, rect.y, rect.width - iconOffset - 5f, rect.height);
            string label = recipe.LabelCap;
            if (recipe.products != null && recipe.products.Count > 0)
            {
                var firstProduct = recipe.products[0];
                if (firstProduct.thingDef != null)
                {
                    label = firstProduct.thingDef.LabelCap;
                    if (firstProduct.count > 1)
                    {
                        label += " (" + firstProduct.count + " шт)";
                    }
                }
            }
            Widgets.Label(labelRect, label);
            
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, recipe.description);
            }
        }

        private void DrawWorkbenchesPanel(Rect rect, float rowHeight)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            
            // Draw title
            Rect titleRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 30f, headerRect.height);
            Widgets.Label(titleRect, "ProductionFlow.Workbenches".Translate());
            
            // Draw "Select All" button (to the right of title)
            float buttonSize = rowHeight + 5f;
            Rect selectAllButtonRect = new Rect(headerRect.xMax - buttonSize - 5f, headerRect.y, buttonSize, buttonSize);
            Texture2D selectAllIcon = ContentFinder<Texture2D>.Get("UI/Buttons/SelectAll", false);
            if (selectAllIcon == null)
            {
                selectAllIcon = ContentFinder<Texture2D>.Get("UI/Commands/SelectAll", false);
            }
            if (selectAllIcon == null)
            {
                // Fallback to text button if no icon available
                if (Widgets.ButtonText(selectAllButtonRect, "✓"))
                {
                    List<Thing> availableWorkbenches = GetAvailableWorkbenches();
                    // Check if all workbenches are selected
                    bool allSelected = availableWorkbenches.Count > 0 && 
                                       availableWorkbenches.All(wb => selectedWorkbenches.Contains(wb));
                    
                    if (allSelected)
                    {
                        // Deselect all
                        selectedWorkbenches.Clear();
                    }
                    else
                    {
                        // Select all
                        selectedWorkbenches.Clear();
                        foreach (Thing workbench in availableWorkbenches)
                        {
                            selectedWorkbenches.Add(workbench);
                        }
                    }
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
                if (Mouse.IsOver(selectAllButtonRect))
                {
                    TooltipHandler.TipRegion(selectAllButtonRect, "ProductionFlow.SelectAllWorkbenches".Translate());
                }
            }
            else
            {
                if (Widgets.ButtonImage(selectAllButtonRect, selectAllIcon))
                {
                    List<Thing> availableWorkbenches = GetAvailableWorkbenches();
                    // Check if all workbenches are selected
                    bool allSelected = availableWorkbenches.Count > 0 && 
                                       availableWorkbenches.All(wb => selectedWorkbenches.Contains(wb));
                    
                    if (allSelected)
                    {
                        // Deselect all
                        selectedWorkbenches.Clear();
                    }
                    else
                    {
                        // Select all
                        selectedWorkbenches.Clear();
                        foreach (Thing workbench in availableWorkbenches)
                        {
                            selectedWorkbenches.Add(workbench);
                        }
                    }
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
                if (Mouse.IsOver(selectAllButtonRect))
                {
                    TooltipHandler.TipRegion(selectAllButtonRect, "ProductionFlow.SelectAllWorkbenches".Translate());
                }
            }
            
            Rect scrollRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, workbenchesScrollHeight);
            
            Widgets.BeginScrollView(scrollRect, ref workbenchesScrollPosition, viewRect);
            
            Dictionary<Map, List<Thing>> workbenchesBySettlement = GetWorkbenchesBySettlement();
            
            if (workbenchesBySettlement.Count == 0)
            {
                Rect noWorkbenchesRect = new Rect(0f, 0f, viewRect.width, rowHeight);
                Widgets.Label(noWorkbenchesRect, "ProductionFlow.NoWorkbenches".Translate());
                workbenchesScrollHeight = rowHeight;
            }
            else
            {
                float yPos = 0f;
                float settlementHeaderHeight = rowHeight * 1.2f;
                float workbenchRowHeight = rowHeight * 1.5f;
                
                foreach (var kvp in workbenchesBySettlement.OrderBy(x => GetSettlementName(x.Key)))
                {
                    Map map = kvp.Key;
                    List<Thing> workbenches = kvp.Value;
                    bool isExpanded = expandedSettlements.Contains(map);
                    
                    // Draw settlement header
                    Rect settlementHeaderRect = new Rect(0f, yPos, viewRect.width, settlementHeaderHeight);
                    if (yPos - workbenchesScrollPosition.y + settlementHeaderHeight >= 0f && yPos - workbenchesScrollPosition.y <= scrollRect.height)
                    {
                        DrawSettlementHeader(settlementHeaderRect, map, workbenches, rowHeight);
                    }
                    yPos += settlementHeaderHeight + 2f;
                    
                    // Draw workbenches if expanded
                    if (isExpanded)
                    {
                        foreach (Thing workbench in workbenches.OrderBy(w => w.LabelCap))
                        {
                            Rect workbenchRect = new Rect(0f, yPos, viewRect.width, workbenchRowHeight);
                            
                            if (yPos - workbenchesScrollPosition.y + workbenchRowHeight >= 0f && yPos - workbenchesScrollPosition.y <= scrollRect.height)
                            {
                                DrawWorkbenchRow(workbenchRect, workbench, rowHeight);
                            }
                            
                            yPos += workbenchRowHeight + 2f;
                        }
                    }
                }
                workbenchesScrollHeight = yPos;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawSettlementHeader(Rect rect, Map map, List<Thing> workbenches, float rowHeight)
        {
            bool isExpanded = expandedSettlements.Contains(map);
            bool isHovered = Mouse.IsOver(rect);
            
            if (isHovered)
            {
                Widgets.DrawHighlight(rect);
            }
            
            float expandButtonSize = rect.height * 0.7f;
            Rect expandButtonRect = new Rect(rect.x + 5f, rect.y + (rect.height - expandButtonSize) / 2f, expandButtonSize, expandButtonSize);
            
            // Handle expand/collapse click (only on expand button area)
            if (Widgets.ButtonInvisible(expandButtonRect))
            {
                if (isExpanded)
                {
                    expandedSettlements.Remove(map);
                }
                else
                {
                    expandedSettlements.Add(map);
                }
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            
            // Draw expand/collapse icon
            Texture2D expandIcon = isExpanded ? TexButton.Collapse : TexButton.Reveal;
            if (expandIcon != null)
            {
                GUI.DrawTexture(expandButtonRect, expandIcon);
            }
            
            // Draw checkbox for selecting all workbenches in settlement
            float checkboxSize = rowHeight * 0.8f;
            float checkboxOffset = expandButtonSize + 10f;
            Rect checkboxRect = new Rect(rect.x + checkboxOffset, rect.y + (rect.height - checkboxSize) / 2f, checkboxSize, checkboxSize);
            
            // Check if all workbenches in this settlement are selected
            bool allSelected = workbenches.Count > 0 && workbenches.All(wb => selectedWorkbenches.Contains(wb));
            bool checkboxState = allSelected;
            
            Widgets.Checkbox(checkboxRect.position, ref checkboxState);
            
            // Handle checkbox state change
            if (checkboxState != allSelected)
            {
                if (checkboxState)
                {
                    // Select all workbenches in this settlement
                    foreach (Thing workbench in workbenches)
                    {
                        selectedWorkbenches.Add(workbench);
                    }
                }
                else
                {
                    // Deselect all workbenches in this settlement
                    foreach (Thing workbench in workbenches)
                    {
                        selectedWorkbenches.Remove(workbench);
                    }
                }
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            
            // Draw settlement name and workbench count
            float labelOffset = checkboxOffset + checkboxSize + 10f;
            Rect labelRect = new Rect(rect.x + labelOffset, rect.y, rect.width - labelOffset - 5f, rect.height);
            string settlementName = GetSettlementName(map);
            string labelText = settlementName + " (" + workbenches.Count + ")";
            Widgets.Label(labelRect, labelText);
        }

        private void DrawWorkbenchRow(Rect rect, Thing workbench, float rowHeight)
        {
            bool isSelectedForBills = selectedWorkbenchForBills == workbench;
            bool isSelected = selectedWorkbenches.Contains(workbench);
            bool isHovered = Mouse.IsOver(rect);
            
            if (isHovered)
            {
                hoveredWorkbench = workbench;
                Widgets.DrawHighlight(rect);
            }
            
            // Highlight selected workbench for bills
            if (isSelectedForBills)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.5f, 0.3f, 0.3f));
            }
            
            // Add indentation for workbenches under settlements
            float indent = 25f;
            float checkboxSize = rowHeight * 0.8f;
            Rect checkboxRect = new Rect(rect.x + indent + 5f, rect.y + (rect.height - checkboxSize) / 2f, checkboxSize, checkboxSize);
            Widgets.Checkbox(checkboxRect.position, ref isSelected);
            
            if (isSelected != selectedWorkbenches.Contains(workbench))
            {
                if (isSelected)
                {
                    selectedWorkbenches.Add(workbench);
                }
                else
                {
                    selectedWorkbenches.Remove(workbench);
                }
            }
            
            // Draw workbench icon
            float iconSize = rowHeight * 0.8f;
            float iconOffset = indent + checkboxSize + 10f;
            
            if (workbench.def.uiIcon != null)
            {
                Rect iconRect = new Rect(rect.x + iconOffset, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, workbench.def.uiIcon);
                iconOffset += iconSize + 5f;
            }
            
            // Calculate search button size and position (before clickRect calculation)
            float buttonSize = rowHeight * 0.8f;
            float buttonRightMargin = 5f;
            Rect searchButtonRect = new Rect(rect.xMax - buttonSize - buttonRightMargin, rect.y + (rect.height - buttonSize) / 2f, buttonSize, buttonSize);
            
            // Adjust clickRect to exclude search button area (accounting for indent)
            Rect clickRect = new Rect(checkboxRect.xMax + 5f, rect.y, rect.width - (checkboxRect.xMax - rect.x) - buttonSize - buttonRightMargin, rect.height);
            if (Widgets.ButtonInvisible(clickRect))
            {
                if (selectedWorkbenchForBills == workbench)
                {
                    selectedWorkbenchForBills = null; // Deselect if clicking again
                }
                else
                {
                    selectedWorkbenchForBills = workbench; // Select this workbench
                }
            }
            
            // Adjust label rect to leave space for button
            Rect labelRect = new Rect(rect.x + iconOffset, rect.y, rect.width - iconOffset - buttonSize - buttonRightMargin - 5f, rect.height);
            Widgets.Label(labelRect, workbench.LabelCap);
            
            // Show indicator if this workbench is selected for bills (positioned before button)
            if (isSelectedForBills)
            {
                Rect indicatorRect = new Rect(searchButtonRect.x - 15f, rect.y + (rect.height - 10f) / 2f, 10f, 10f);
                Widgets.DrawBoxSolid(indicatorRect, Color.green);
            }
            
            // Search workbench button (separate click zone)
            Texture2D icon = SearchIcon;
            if (icon != null)
            {
                if (Widgets.ButtonImage(searchButtonRect, icon))
                {
                    if (workbench != null && workbench.Spawned && workbench.Map != null)
                    {
                        // Select and jump to workbench
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(workbench);
                        CameraJumper.TryJump(workbench);
                    }
                }
            }
            else
            {
                // Fallback to text button if no icon available
                if (Widgets.ButtonText(searchButtonRect, "→"))
                {
                    if (workbench != null && workbench.Spawned && workbench.Map != null)
                    {
                        // Select and jump to workbench
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(workbench);
                        CameraJumper.TryJump(workbench);
                    }
                }
            }
            
            if (Mouse.IsOver(searchButtonRect))
            {
                TooltipHandler.TipRegion(searchButtonRect, "ProductionFlow.JumpToWorkbench".Translate());
            }
        }

        private void DrawRecipeInfoPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            // Calculate row height for this panel
            float rowHeight = Text.CalcSize("A").y * 1.1f;
            float minRowHeight = 20f;
            rowHeight = Mathf.Max(rowHeight, minRowHeight);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            Widgets.Label(headerRect, "ProductionFlow.RecipeInfo".Translate());
            
            // Add info card button (similar to Dialog_BillConfig)
            if (selectedRecipe != null && selectedRecipe.products != null && selectedRecipe.products.Count == 1)
            {
                ThingDef thingDef = selectedRecipe.products[0].thingDef;
                if (thingDef != null)
                {
                    Widgets.InfoCardButton(headerRect.xMax - 24f, headerRect.y, thingDef, GenStuff.DefaultStuffFor(thingDef));
                }
            }
            
            Rect scrollRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, recipeInfoScrollHeight);
            
            Widgets.BeginScrollView(scrollRect, ref recipeInfoScrollPosition, viewRect);
            
            if (selectedRecipe == null)
            {
                Rect noRecipeRect = new Rect(0f, 0f, viewRect.width, rowHeight);
                Widgets.Label(noRecipeRect, "ProductionFlow.SelectRecipe".Translate());
                recipeInfoScrollHeight = rowHeight;
            }
            else
            {
                float width = viewRect.width;
                
                // Build recipe information string using the same format as Dialog_BillConfig
                StringBuilder stringBuilder = new StringBuilder();
                
                // Description
                if (!string.IsNullOrEmpty(selectedRecipe.description))
                {
                    stringBuilder.AppendLine(selectedRecipe.description);
                    stringBuilder.AppendLine();
                }
                
                // Work amount (same format as Dialog_BillConfig)
                stringBuilder.AppendLine("WorkAmount".Translate() + ": " + selectedRecipe.WorkAmountTotal(null).ToStringWorkAmount());
                
                // Ingredients (same format as Dialog_BillConfig)
                for (int i = 0; i < selectedRecipe.ingredients.Count; i++)
                {
                    IngredientCount ingredientCount = selectedRecipe.ingredients[i];
                    if (!ingredientCount.filter.Summary.NullOrEmpty())
                    {
                        stringBuilder.AppendLine(selectedRecipe.IngredientValueGetter.BillRequirementsDescription(selectedRecipe, ingredientCount));
                    }
                }
                
                // Extra description line (same format as Dialog_BillConfig)
                string extraDescriptionLine = selectedRecipe.IngredientValueGetter.ExtraDescriptionLine(selectedRecipe);
                if (extraDescriptionLine != null)
                {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine(extraDescriptionLine);
                }
                
                // Minimum skills (same format as Dialog_BillConfig)
                if (!selectedRecipe.skillRequirements.NullOrEmpty())
                {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("MinimumSkills".Translate());
                    stringBuilder.AppendLine(selectedRecipe.MinSkillString);
                }
                
                // Display the text
                Text.Font = GameFont.Small;
                string text = stringBuilder.ToString();
                float textHeight = Text.CalcHeight(text, width);
                if (textHeight > viewRect.height)
                {
                    Text.Font = GameFont.Tiny;
                    textHeight = Text.CalcHeight(text, width);
                }
                
                Widgets.Label(new Rect(0f, 0f, width, textHeight), text);
                Text.Font = GameFont.Small;
                
                recipeInfoScrollHeight = textHeight;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawBillCreationControls(Rect rect, float controlHeight)
        {
            float yPos = rect.y;
            float width = rect.width - 10f;
            float xPos = rect.x + 5f;
            
            // Quality (if applicable) - top
            if (selectedRecipe != null && selectedRecipe.workSkill != null)
            {
                Rect qualityRect = new Rect(xPos, yPos, width, controlHeight);
                string qualityLabel = selectedQuality.HasValue 
                    ? selectedQuality.Value.GetLabel() 
                    : "ProductionFlow.AnyQuality".Translate();
                
                Widgets.Label(new Rect(qualityRect.x, qualityRect.y, qualityRect.width * 0.4f, qualityRect.height), "ProductionFlow.Quality".Translate());
                if (Widgets.ButtonText(new Rect(qualityRect.x + qualityRect.width * 0.4f, qualityRect.y, qualityRect.width * 0.6f, qualityRect.height), qualityLabel))
                {
                    Find.WindowStack.Add(new FloatMenu(GetQualityOptions()));
                }
                yPos += controlHeight + 5f;
            }
            
            // Repeat mode selection
            string repeatModeLabel = GetRepeatModeLabel(selectedRepeatMode);
            Rect repeatModeRect = new Rect(xPos, yPos, width, controlHeight);
            if (Widgets.ButtonText(repeatModeRect, repeatModeLabel))
            {
                Find.WindowStack.Add(new FloatMenu(GetRepeatModeOptions()));
            }
            yPos += controlHeight + 5f;
            
            // Quantity/Target count input (shown based on selected mode)
            if (selectedRepeatMode == BillRepeatModeDefOf.RepeatCount || selectedRepeatMode == BillRepeatModeDefOf.TargetCount)
            {
                Rect inputRect = new Rect(xPos, yPos, width, controlHeight);
                string label = selectedRepeatMode == BillRepeatModeDefOf.RepeatCount 
                    ? "ProductionFlow.Quantity".Translate() 
                    : "ProductionFlow.TargetCount".Translate();
                Widgets.Label(new Rect(inputRect.x, inputRect.y, inputRect.width * 0.4f, inputRect.height), label);
                
                string buffer = selectedRepeatMode == BillRepeatModeDefOf.RepeatCount ? quantityBuffer : targetCountBuffer;
                buffer = Widgets.TextField(new Rect(inputRect.x + inputRect.width * 0.4f, inputRect.y, inputRect.width * 0.6f, inputRect.height), buffer);
                
                if (selectedRepeatMode == BillRepeatModeDefOf.RepeatCount)
                {
                    quantityBuffer = buffer;
                    if (int.TryParse(buffer, out int parsedQuantity) && parsedQuantity > 0)
                    {
                        quantity = parsedQuantity;
                    }
                }
                else
                {
                    targetCountBuffer = buffer;
                    if (int.TryParse(buffer, out int parsedTarget) && parsedTarget > 0)
                    {
                        targetCount = parsedTarget;
                    }
                }
                yPos += controlHeight + 5f;
            }
            
            // Create bills button with smart button next to it
            float smartButtonSize = controlHeight; // Square button, same height as main button
            float mainButtonWidth = width - smartButtonSize - 5f; // Reduce main button width by smart button size + margin
            
            Rect buttonRect = new Rect(xPos, yPos, mainButtonWidth, controlHeight);
            if (Widgets.ButtonText(buttonRect, "ProductionFlow.CreateBills".Translate()))
            {
                CreateBills();
            }
            
            // Smart create button (square, to the right of main button)
            Rect smartButtonRect = new Rect(xPos + mainButtonWidth + 5f, yPos, smartButtonSize, smartButtonSize);
            Texture2D smartIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Auto", false);
            if (smartIcon == null)
            {
                smartIcon = ContentFinder<Texture2D>.Get("UI/Commands/Auto", false);
            }
            if (smartIcon == null)
            {
                // Fallback to text button
                if (Widgets.ButtonText(smartButtonRect, "⚡"))
                {
                    CreateBillsWithMaterials();
                }
            }
            else
            {
                if (Widgets.ButtonImage(smartButtonRect, smartIcon))
                {
                    CreateBillsWithMaterials();
                }
            }
            if (Mouse.IsOver(smartButtonRect))
            {
                TooltipHandler.TipRegion(smartButtonRect, "ProductionFlow.CreateBillsWithMaterials".Translate());
            }
        }
        
        private string GetRepeatModeLabel(BillRepeatModeDef mode)
        {
            if (mode == BillRepeatModeDefOf.RepeatCount)
            {
                return "ProductionFlow.RepeatMode.RepeatCount".Translate(quantity);
            }
            else if (mode == BillRepeatModeDefOf.TargetCount)
            {
                return "ProductionFlow.RepeatMode.TargetCount".Translate(targetCount);
            }
            else if (mode == BillRepeatModeDefOf.Forever)
            {
                return "ProductionFlow.RepeatMode.Forever".Translate();
            }
            return mode.label;
        }
        
        private List<FloatMenuOption> GetRepeatModeOptions()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            options.Add(new FloatMenuOption("ProductionFlow.RepeatMode.RepeatCount".Translate(quantity), () => { 
                selectedRepeatMode = BillRepeatModeDefOf.RepeatCount; 
            }));
            
            options.Add(new FloatMenuOption("ProductionFlow.RepeatMode.TargetCount".Translate(targetCount), () => { 
                selectedRepeatMode = BillRepeatModeDefOf.TargetCount; 
            }));
            
            options.Add(new FloatMenuOption("ProductionFlow.RepeatMode.Forever".Translate(), () => { 
                selectedRepeatMode = BillRepeatModeDefOf.Forever; 
            }));
            
            return options;
        }
        
        private void DrawMaterialsPanel(Rect rect, float rowHeight)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            Widgets.Label(headerRect, "ProductionFlow.Materials".Translate());
            
            Rect filterRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
            
            if (selectedRecipe == null)
            {
                Rect noRecipeRect = new Rect(filterRect.x, filterRect.y, filterRect.width, rowHeight);
                Widgets.Label(noRecipeRect, "ProductionFlow.SelectRecipeForMaterials".Translate());
            }
            else if (selectedRecipe.ingredients == null || selectedRecipe.ingredients.Count == 0)
            {
                Rect noMaterialsRect = new Rect(filterRect.x, filterRect.y, filterRect.width, rowHeight);
                Widgets.Label(noMaterialsRect, "ProductionFlow.NoMaterialsRequired".Translate());
            }
            else
            {
                // Initialize parent filter from recipe's fixedIngredientFilter (same as RimWorld uses in Dialog_BillConfig)
                // This restricts what materials can be selected - only those allowed by the recipe
                ThingFilter recipeFixedIngredientFilter = selectedRecipe.fixedIngredientFilter;
                
                // Initialize material filter if needed or if recipe changed
                // Set all allowed materials as selected by default
                if (materialFilter == null || materialFilterRecipe != selectedRecipe)
                {
                    // Create temporary bill to get properly initialized ingredientFilter
                    // Use MakeNewBill() for consistency (same as RimWorld)
                    Bill tempBill = selectedRecipe.MakeNewBill();
                    materialFilter = new ThingFilter();
                    // Copy all allowances from bill's ingredientFilter (all materials allowed by default)
                    if (tempBill.ingredientFilter != null)
                    {
                        materialFilter.CopyAllowancesFrom(tempBill.ingredientFilter);
                    }
                    materialFilterRecipe = selectedRecipe;
                }
                
                // Draw standard ThingFilterUI with parent filter to restrict available materials
                // Use recipe.fixedIngredientFilter as parent filter, exactly like RimWorld does in Dialog_BillConfig
                ThingFilterUI.DoThingFilterConfigWindow(
                    filterRect,
                    materialFilterUIState,
                    materialFilter,
                    recipeFixedIngredientFilter // Parent filter restricts what materials are shown in UI
                );
            }
        }

        private void DrawPawnsPanel(Rect rect, float rowHeight)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            Widgets.Label(headerRect, "ProductionFlow.Settlers".Translate());
            
            Rect scrollRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, pawnsScrollHeight);
            
            Widgets.BeginScrollView(scrollRect, ref pawnsScrollPosition, viewRect);
            
            List<Pawn> pawns = GetAvailablePawns();
            
            if (pawns.Count == 0)
            {
                Rect noPawnsRect = new Rect(0f, 0f, viewRect.width, rowHeight);
                Widgets.Label(noPawnsRect, "ProductionFlow.NoSettlers".Translate());
                pawnsScrollHeight = rowHeight;
            }
            else
            {
                float yPos = 0f;
                float pawnRowHeight = rowHeight * 1.5f;
                foreach (Pawn pawn in pawns)
                {
                    Rect pawnRect = new Rect(0f, yPos, viewRect.width, pawnRowHeight);
                    
                    if (yPos - pawnsScrollPosition.y + pawnRowHeight >= 0f && yPos - pawnsScrollPosition.y <= scrollRect.height)
                    {
                        DrawPawnRow(pawnRect, pawn, rowHeight);
                    }
                    
                    yPos += pawnRowHeight + 2f;
                }
                pawnsScrollHeight = yPos;
            }
            
            Widgets.EndScrollView();
        }

        private void DrawPawnRow(Rect rect, Pawn pawn, float rowHeight)
        {
            bool isSelected = selectedPawns.Contains(pawn);
            
            float checkboxSize = rowHeight * 0.8f;
            Rect checkboxRect = new Rect(rect.x + 5f, rect.y + (rect.height - checkboxSize) / 2f, checkboxSize, checkboxSize);
            Widgets.Checkbox(checkboxRect.position, ref isSelected);
            
            if (isSelected != selectedPawns.Contains(pawn))
            {
                if (isSelected)
                {
                    selectedPawns.Add(pawn);
                }
                else
                {
                    selectedPawns.Remove(pawn);
                }
            }
            
            // Pawn portrait/icon
            float iconSize = rowHeight * 0.9f;
            float iconOffset = checkboxSize + 10f;
            Rect iconRect = new Rect(rect.x + iconOffset, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
            
            if (pawn != null)
            {
                RenderTexture portrait = PortraitsCache.Get(pawn, new Vector2(iconSize, iconSize), Rot4.South, new Vector3(0f, 0f, 0.1f));
                if (portrait != null)
                {
                    GUI.DrawTexture(iconRect, portrait);
                }
                iconOffset += iconSize + 5f;
            }
            
            // Pawn name
            Rect nameRect = new Rect(rect.x + iconOffset, rect.y, rect.width - iconOffset - 5f, rowHeight);
            Widgets.Label(nameRect, pawn.LabelCap);
            
            // Skill level (if recipe requires skill)
            if (selectedRecipe != null && selectedRecipe.workSkill != null)
            {
                SkillRecord skill = pawn.skills?.GetSkill(selectedRecipe.workSkill);
                if (skill != null)
                {
                    Rect skillRect = new Rect(rect.x + checkboxSize + 10f, rect.y + rowHeight, rect.width - checkboxSize - 15f, rowHeight * 0.8f);
                    string skillText = selectedRecipe.workSkill.label + ": " + skill.Level;
                    int minSkill = 0;
                    if (selectedRecipe.skillRequirements != null)
                    {
                        var skillReq = selectedRecipe.skillRequirements.FirstOrDefault(sr => sr.skill == selectedRecipe.workSkill);
                        if (skillReq != null)
                        {
                            minSkill = skillReq.minLevel;
                        }
                    }
                    if (minSkill > 0 && skill.Level < minSkill)
                    {
                        GUI.color = Color.red;
                    }
                    Widgets.Label(skillRect, skillText);
                    GUI.color = Color.white;
                }
            }
            
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
        }

        private List<RecipeDef> GetAllRecipes()
        {
            // Cache results per frame to avoid repeated expensive operations
            int currentFrame = Time.frameCount;
            if (cachedAllRecipes != null && cachedAllRecipesFrame == currentFrame)
            {
                return cachedAllRecipes;
            }
            
            List<RecipeDef> recipes = new List<RecipeDef>();
            
            if (Current.Game == null)
                return recipes;
            
            HashSet<RecipeDef> uniqueRecipes = new HashSet<RecipeDef>();
            
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null || map.listerThings == null)
                    continue;
                
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing is IBillGiver && thing.def.building != null && thing.Spawned)
                    {
                        if (thing.def.AllRecipes != null)
                        {
                            foreach (RecipeDef recipe in thing.def.AllRecipes)
                            {
                                if (recipe.products != null && recipe.products.Count > 0)
                                {
                                    uniqueRecipes.Add(recipe);
                                }
                            }
                        }
                    }
                }
            }
            
            cachedAllRecipes = uniqueRecipes.ToList();
            cachedAllRecipesFrame = currentFrame;
            return cachedAllRecipes;
        }

        private List<Thing> GetAvailableWorkbenches()
        {
            if (selectedRecipe == null)
                return new List<Thing>();
            
            List<Thing> workbenches = new List<Thing>();
            
            if (Current.Game == null)
                return workbenches;
            
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null || map.listerThings == null)
                    continue;
                    
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing is IBillGiver && thing.def.building != null && thing.Spawned)
                    {
                        if (thing.def.AllRecipes != null && thing.def.AllRecipes.Contains(selectedRecipe))
                    {
                        workbenches.Add(thing);
                        }
                    }
                }
            }
            
            return workbenches;
        }
        
        private Dictionary<Map, List<Thing>> GetWorkbenchesBySettlement()
        {
            Dictionary<Map, List<Thing>> workbenchesBySettlement = new Dictionary<Map, List<Thing>>();
            
            if (selectedRecipe == null)
                return workbenchesBySettlement;
            
            if (Current.Game == null)
                return workbenchesBySettlement;
            
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null || map.listerThings == null)
                    continue;
                
                List<Thing> workbenches = new List<Thing>();
                
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing is IBillGiver && thing.def.building != null && thing.Spawned)
                    {
                        if (thing.def.AllRecipes != null && thing.def.AllRecipes.Contains(selectedRecipe))
                        {
                            workbenches.Add(thing);
                        }
                    }
                }
                
                if (workbenches.Count > 0)
                {
                    workbenchesBySettlement[map] = workbenches;
                }
            }
            
            return workbenchesBySettlement;
        }
        
        private string GetSettlementName(Map map)
        {
            if (map == null)
                return "ProductionFlow.UnknownSettlement".Translate();
            
            if (map.info != null && map.info.parent != null)
            {
                return map.info.parent.Label;
            }
            
            // Fallback to map index if no parent
            return "ProductionFlow.Map".Translate() + " " + (Current.Game.Maps.IndexOf(map) + 1);
        }

        private List<Pawn> GetAvailablePawns()
        {
            if (selectedRecipe == null)
                return new List<Pawn>();
            
            List<Pawn> pawns = new List<Pawn>();
            
            if (Current.Game == null)
                return pawns;
            
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null)
                    continue;
                
                foreach (Pawn pawn in map.mapPawns.FreeColonists)
                {
                    if (pawn == null || pawn.Dead || pawn.Downed)
                        continue;
                    
                    bool canDoRecipe = false;
                    
                    // Check if pawn can perform the recipe
                    // First check all skill requirements
                    bool meetsSkillRequirements = true;
                    if (selectedRecipe.skillRequirements != null && selectedRecipe.skillRequirements.Count > 0)
                    {
                        foreach (SkillRequirement skillReq in selectedRecipe.skillRequirements)
                        {
                            if (skillReq.skill != null)
                            {
                                SkillRecord skill = pawn.skills?.GetSkill(skillReq.skill);
                                if (skill == null || skill.Level < skillReq.minLevel)
                                {
                                    meetsSkillRequirements = false;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (meetsSkillRequirements)
                    {
                        // Check if recipe has work skill requirement
                        if (selectedRecipe.workSkill != null)
                        {
                            // Find work type that uses this skill (usually Crafting, but could be others)
                            WorkTypeDef workType = WorkTypeDefOf.Crafting; // Default to crafting
                            
                            // Try to find the appropriate work type for this skill
                            foreach (WorkTypeDef workTypeDef in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                            {
                                if (workTypeDef.relevantSkills != null && workTypeDef.relevantSkills.Contains(selectedRecipe.workSkill))
                                {
                                    workType = workTypeDef;
                                    break;
                                }
                            }
                            
                            // Check if pawn has work enabled for this work type
                            if (pawn.workSettings != null && pawn.workSettings.GetPriority(workType) > 0)
                            {
                                canDoRecipe = true;
                            }
                        }
                        else
                        {
                            // Recipe doesn't require specific skill, check if pawn can do crafting
                            if (pawn.workSettings != null && pawn.workSettings.GetPriority(WorkTypeDefOf.Crafting) > 0)
                            {
                                canDoRecipe = true;
                            }
                        }
                    }
                    
                    if (canDoRecipe)
                    {
                        pawns.Add(pawn);
                    }
                }
            }
            
            return pawns;
        }

        private bool IsBillCompleted(Bill bill)
        {
            if (bill is Bill_Production billProduction)
            {
                if (billProduction.repeatMode == BillRepeatModeDefOf.RepeatCount)
                {
                    // Completed when repeatCount reaches 0
                    return billProduction.repeatCount <= 0;
                }
                else if (billProduction.repeatMode == BillRepeatModeDefOf.TargetCount)
                {
                    // Don't remove TargetCount bills - they maintain stockpile quantity
                    // RimWorld will automatically delete them when target is reached
                    return false;
                }
                // Forever mode is never completed
                return false;
            }
            return false;
        }
        
        private int ClearCompletedBillsFromWorkbench(IBillGiver billGiver)
        {
            if (billGiver?.BillStack == null)
                return 0;
            
            int clearedCount = 0;
            List<Bill> billsToRemove = new List<Bill>();
            
            foreach (Bill bill in billGiver.BillStack)
            {
                if (IsBillCompleted(bill))
                {
                    billsToRemove.Add(bill);
                }
            }
            
            foreach (Bill bill in billsToRemove)
            {
                billGiver.BillStack.Delete(bill);
                clearedCount++;
            }
            
            return clearedCount;
        }
        
        private int ClearCompletedBillsFromAllWorkbenches()
        {
            int totalCleared = 0;
            
            if (Current.Game == null)
                return totalCleared;
            
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null || map.listerThings == null)
                    continue;
                
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing is IBillGiver billGiver && thing.def.building != null && thing.Spawned)
                    {
                        totalCleared += ClearCompletedBillsFromWorkbench(billGiver);
                    }
                }
            }
            
            return totalCleared;
        }
        
        private void DrawWorkbenchBillsPanel(Rect rect, float rowHeight)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            
            // Draw title
            Rect titleRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 30f, headerRect.height);
            Widgets.Label(titleRect, "ProductionFlow.WorkbenchBills".Translate());
            
            // Draw clear button (to the right of title)
            float buttonSize = rowHeight + 5f;
            Rect clearButtonRect = new Rect(headerRect.xMax - buttonSize - 5f, headerRect.y, buttonSize, buttonSize);
            Texture2D clearIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Delete", false);
            if (clearIcon == null)
            {
                clearIcon = ContentFinder<Texture2D>.Get("UI/Commands/Cancel", false);
            }
            if (clearIcon != null)
            {
                if (Widgets.ButtonImage(clearButtonRect, clearIcon))
                {
                    int clearedCount = ClearCompletedBillsFromAllWorkbenches();
                    if (clearedCount > 0)
                    {
                        Messages.Message("ProductionFlow.CompletedBillsCleared".Translate(clearedCount), MessageTypeDefOf.PositiveEvent);
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }
                    else
                    {
                        Messages.Message("ProductionFlow.NoCompletedBills".Translate(), MessageTypeDefOf.NeutralEvent);
                    }
                }
                if (Mouse.IsOver(clearButtonRect))
                {
                    TooltipHandler.TipRegion(clearButtonRect, "ProductionFlow.ClearCompletedBills".Translate());
                }
            }
            
            // Use selected workbench for bills, fallback to hovered if none selected
            Thing workbenchToShow = selectedWorkbenchForBills != null ? selectedWorkbenchForBills : hoveredWorkbench;
            
            if (workbenchToShow == null)
            {
                Rect noWorkbenchRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rowHeight);
                Widgets.Label(noWorkbenchRect, "ProductionFlow.SelectWorkbenchForBills".Translate());
            }
            else if (workbenchToShow is IBillGiver billGiver && billGiver.BillStack != null)
            {
                // Standard bills listing area (similar to ITab_Bills)
                Rect billsRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
                billsRect = billsRect.ContractedBy(5f);
                
                // Paste button (like ITab_Bills)
                float pasteX = billsRect.width - 48f;
                float pasteY = 3f;
                float pasteSize = 24f;
                Rect pasteRect = new Rect(billsRect.x + pasteX, billsRect.y + pasteY, pasteSize, pasteSize);
                
                if (BillUtility.Clipboard == null)
                {
                    GUI.color = Color.gray;
                    Widgets.DrawTextureFitted(pasteRect, TexButton.Paste, 1f);
                    GUI.color = Color.white;
                    TooltipHandler.TipRegionByKey(pasteRect, "PasteBillTip");
                }
                else
                {
                    Thing workbenchThing = workbenchToShow as Thing;
                    bool canPaste = workbenchThing != null 
                        && workbenchThing.def.AllRecipes != null
                        && workbenchThing.def.AllRecipes.Contains(BillUtility.Clipboard.recipe) 
                        && BillUtility.Clipboard.recipe.AvailableNow 
                        && BillUtility.Clipboard.recipe.AvailableOnNow(workbenchThing)
                        && billGiver.BillStack.Count < 15;
                    
                    if (!canPaste)
                    {
                        GUI.color = Color.gray;
                        Widgets.DrawTextureFitted(pasteRect, TexButton.Paste, 1f);
                        GUI.color = Color.white;
                        if (billGiver.BillStack.Count >= 15)
                        {
                            if (Mouse.IsOver(pasteRect))
                            {
                                TooltipHandler.TipRegion(pasteRect, "PasteBillTip".Translate() + " (" + "PasteBillTip_LimitReached".Translate() + ")");
                            }
                        }
                        else
                        {
                            TooltipHandler.TipRegionByKey(pasteRect, "ClipboardBillNotAvailableHere");
                        }
                    }
                    else
                    {
                        if (Widgets.ButtonImageFitted(pasteRect, TexButton.Paste, Color.white))
                        {
                        Bill bill = BillUtility.Clipboard.Clone();
                        bill.InitializeAfterClone();
                        AddBillAtStart(billGiver, bill);
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                        }
                        TooltipHandler.TipRegionByKey(pasteRect, "PasteBillTip");
                    }
                }
                
                // Recipe options maker (like ITab_Bills)
                Thing workbenchThingForRecipes = workbenchToShow as Thing;
                Func<List<FloatMenuOption>> recipeOptionsMaker = delegate
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();
                    
                    if (workbenchThingForRecipes != null && workbenchThingForRecipes.def.AllRecipes != null)
                    {
                        for (int i = 0; i < workbenchThingForRecipes.def.AllRecipes.Count; i++)
                        {
                            RecipeDef recipe = workbenchThingForRecipes.def.AllRecipes[i];
                            if (recipe.AvailableNow && recipe.AvailableOnNow(workbenchThingForRecipes))
                            {
                                RecipeDef localRecipe = recipe;
                                ThingDef uiIconThing = localRecipe.UIIconThing;
                                string label = localRecipe.LabelCap.ToString();
                                Action action = delegate
                                {
                                    Map map = workbenchThingForRecipes.Map;
                                    if (map != null && !map.mapPawns.FreeColonists.Any((Pawn col) => localRecipe.PawnSatisfiesSkillRequirements(col)))
                                    {
                                        Bill.CreateNoPawnsWithSkillDialog(localRecipe);
                                    }
                                    Bill bill2 = localRecipe.MakeNewBill();
                                    AddBillAtStart(billGiver, bill2);
                                    if (localRecipe.conceptLearned != null)
                                    {
                                        PlayerKnowledgeDatabase.KnowledgeDemonstrated(localRecipe.conceptLearned, KnowledgeAmount.Total);
                                    }
                                    if (TutorSystem.TutorialMode)
                                    {
                                        TutorSystem.Notify_Event("AddBill-" + localRecipe.LabelCap.Resolve());
                                    }
                                };
                                Func<Rect, bool> extraPartOnGUI = (Rect menuRect) => Widgets.InfoCardButton(menuRect.x + 5f, menuRect.y + (menuRect.height - 24f) / 2f, localRecipe);
                                list.Add(new FloatMenuOption(
                                    label: label,
                                    action: action,
                                    shownItemForIcon: uiIconThing,
                                    priority: MenuOptionPriority.Default,
                                    mouseoverGuiAction: null,
                                    revalidateClickTarget: null,
                                    extraPartWidth: 29f,
                                    extraPartOnGUI: extraPartOnGUI,
                                    revalidateWorldClickTarget: null));
                            }
                        }
                    }
                    
                    if (!list.Any())
                    {
                        list.Add(new FloatMenuOption("NoneBrackets".Translate(), null));
                    }
                    return list;
                };
                
                // Use standard BillStack.DoListing (like ITab_Bills)
                mouseoverBill = billGiver.BillStack.DoListing(billsRect, recipeOptionsMaker, ref workbenchBillsScrollPosition, ref workbenchBillsScrollHeight);
            }
            else
            {
                Rect invalidRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rowHeight);
                Widgets.Label(invalidRect, "ProductionFlow.InvalidWorkbench".Translate());
            }
        }

        private List<FloatMenuOption> GetQualityOptions()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            options.Add(new FloatMenuOption("ProductionFlow.AnyQuality".Translate(), () => { selectedQuality = null; }));
            
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)).Cast<QualityCategory>())
            {
                QualityCategory localQuality = quality;
                options.Add(new FloatMenuOption(localQuality.GetLabel(), () => { selectedQuality = localQuality; }));
            }
            
            return options;
        }

        private void AddBillAtStart(IBillGiver billGiver, Bill bill)
        {
            bill.billStack = billGiver.BillStack;
            billGiver.BillStack.Bills.Insert(0, bill);
        }

        private void CreateBills()
        {
            if (selectedRecipe == null)
            {
                Messages.Message("ProductionFlow.NoRecipeSelected".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
            if (selectedWorkbenches.Count == 0)
            {
                Messages.Message("ProductionFlow.SelectWorkbench".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
            int billsCreated = 0;
            
            foreach (Thing workbench in selectedWorkbenches.ToList())
            {
                if (workbench is IBillGiver billGiver)
                {
                    if (workbench.def.AllRecipes != null && workbench.def.AllRecipes.Contains(selectedRecipe))
                    {
                        // Clear completed bills (quantity = 0) from this workbench before adding new task
                        ClearCompletedBillsFromWorkbench(billGiver);
                        
                        // Use MakeNewBill() extension method (same as RimWorld does in ITab_Bills)
                        // This ensures correct bill type is created (Bill_Production or Bill_ProductionWithUft)
                        Bill bill = selectedRecipe.MakeNewBill();
                        
                        // Cast to Bill_Production to set production-specific properties
                        if (bill is Bill_Production billProduction)
                        {
                            billProduction.SetStoreMode(BillStoreModeDefOf.BestStockpile);
                            billProduction.repeatMode = selectedRepeatMode;
                            
                            if (selectedRepeatMode == BillRepeatModeDefOf.RepeatCount)
                            {
                                billProduction.repeatCount = quantity;
                            }
                            else if (selectedRepeatMode == BillRepeatModeDefOf.TargetCount)
                            {
                                billProduction.targetCount = targetCount;
                            }
                            // Forever mode doesn't need additional settings
                            
                            if (selectedQuality.HasValue && selectedRecipe.workSkill != null)
                            {
                                billProduction.qualityRange = new QualityRange(selectedQuality.Value, selectedQuality.Value);
                            }
                            
                            // Apply material filter if one is selected
                            if (materialFilter != null && billProduction.ingredientFilter != null)
                            {
                                // Copy the selected material filter to the bill's ingredient filter
                                // This sets the allowed materials for production
                                billProduction.ingredientFilter.CopyAllowancesFrom(materialFilter);
                            }
                        }
                        
                        AddBillAtStart(billGiver, bill);
                        billsCreated++;
                    }
                }
            }
            
            if (billsCreated > 0)
            {
                Messages.Message("ProductionFlow.BillsCreated".Translate(billsCreated), MessageTypeDefOf.PositiveEvent);
                selectedWorkbenches.Clear();
            }
            else
            {
                Messages.Message("ProductionFlow.NoBillsCreated".Translate(), MessageTypeDefOf.RejectInput);
            }
        }
        
        private IEnumerable<Widgets.DropdownMenuElement<Zone_Stockpile>> GenerateStockpileInclusion(Bill_Production bill)
        {
            Widgets.DropdownMenuElement<Zone_Stockpile> dropdownMenuElement = new Widgets.DropdownMenuElement<Zone_Stockpile>
            {
                option = new FloatMenuOption("IncludeFromAll".Translate(), delegate
                {
                    var field = bill.GetType().GetField("includeFromZone", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(bill, null);
                    }
                }),
                payload = null
            };
            yield return dropdownMenuElement;
            
            if (bill.billStack?.billGiver?.Map == null)
                yield break;
                
            List<SlotGroup> groupList = bill.billStack.billGiver.Map.haulDestinationManager.AllGroupsListInPriorityOrder;
            int groupCount = groupList.Count;
            for (int i = 0; i < groupCount; i++)
            {
                SlotGroup slotGroup = groupList[i];
                Zone_Stockpile stockpile = slotGroup.parent as Zone_Stockpile;
                if (stockpile != null)
                {
                    Zone_Stockpile localStockpile = stockpile;
                    dropdownMenuElement = new Widgets.DropdownMenuElement<Zone_Stockpile>
                    {
                        option = new FloatMenuOption("IncludeSpecific".Translate(slotGroup.parent.SlotYielderLabel()), delegate
                        {
                            var field = bill.GetType().GetField("includeFromZone", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (field != null)
                            {
                                field.SetValue(bill, localStockpile);
                            }
                        }),
                        payload = stockpile
                    };
                    yield return dropdownMenuElement;
                }
            }
        }
        
        private IEnumerable<Widgets.DropdownMenuElement<Pawn>> GeneratePawnRestrictionOptions(Bill_Production bill)
        {
            var pawnRestrictionField = bill.GetType().GetField("pawnRestriction", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (pawnRestrictionField == null)
                yield break;
                
            Widgets.DropdownMenuElement<Pawn> dropdownMenuElement = new Widgets.DropdownMenuElement<Pawn>
            {
                option = new FloatMenuOption("AnyWorker".Translate(), delegate
                {
                    pawnRestrictionField.SetValue(bill, null);
                }),
                payload = null
            };
            yield return dropdownMenuElement;
            
            SkillDef workSkill = bill.recipe.workSkill;
            IEnumerable<Pawn> allMaps_FreeColonists = PawnsFinder.AllMaps_FreeColonists;
            allMaps_FreeColonists = allMaps_FreeColonists.OrderBy((Pawn pawn) => pawn.LabelShortCap);
            if (workSkill != null)
            {
                allMaps_FreeColonists = allMaps_FreeColonists.OrderByDescending((Pawn pawn) => pawn.skills.GetSkill(bill.recipe.workSkill).Level);
            }
            
            if (bill.billStack?.billGiver == null)
                yield break;
                
            WorkGiverDef workGiver = bill.billStack.billGiver.GetWorkgiver();
            if (workGiver == null)
            {
                yield break;
            }
            
            allMaps_FreeColonists = allMaps_FreeColonists.OrderByDescending((Pawn pawn) => pawn.workSettings.WorkIsActive(workGiver.workType));
            allMaps_FreeColonists = allMaps_FreeColonists.OrderBy((Pawn pawn) => pawn.WorkTypeIsDisabled(workGiver.workType));
            
            foreach (Pawn pawn2 in allMaps_FreeColonists)
            {
                if (pawn2.WorkTypeIsDisabled(workGiver.workType))
                {
                    dropdownMenuElement = new Widgets.DropdownMenuElement<Pawn>
                    {
                        option = new FloatMenuOption(string.Format("{0} ({1})", pawn2.LabelShortCap, "WillNever".Translate(workGiver.verb)), null),
                        payload = pawn2
                    };
                    yield return dropdownMenuElement;
                }
                else if (bill.recipe.workSkill != null && !pawn2.workSettings.WorkIsActive(workGiver.workType))
                {
                    Pawn localPawn = pawn2;
                    dropdownMenuElement = new Widgets.DropdownMenuElement<Pawn>
                    {
                        option = new FloatMenuOption(string.Format("{0} ({1} {2}, {3})", pawn2.LabelShortCap, pawn2.skills.GetSkill(bill.recipe.workSkill).Level, bill.recipe.workSkill.label, "NotAssigned".Translate()), delegate
                        {
                            pawnRestrictionField.SetValue(bill, localPawn);
                        }),
                        payload = pawn2
                    };
                    yield return dropdownMenuElement;
                }
                else if (!pawn2.workSettings.WorkIsActive(workGiver.workType))
                {
                    Pawn localPawn = pawn2;
                    dropdownMenuElement = new Widgets.DropdownMenuElement<Pawn>
                    {
                        option = new FloatMenuOption(string.Format("{0} ({1})", pawn2.LabelShortCap, "NotAssigned".Translate()), delegate
                        {
                            pawnRestrictionField.SetValue(bill, localPawn);
                        }),
                        payload = pawn2
                    };
                    yield return dropdownMenuElement;
                }
                else if (bill.recipe.workSkill != null)
                {
                    Pawn localPawn = pawn2;
                    dropdownMenuElement = new Widgets.DropdownMenuElement<Pawn>
                    {
                        option = new FloatMenuOption(string.Format("{0} ({1} {2})", pawn2.LabelShortCap, pawn2.skills.GetSkill(bill.recipe.workSkill).Level, bill.recipe.workSkill.label), delegate
                        {
                            pawnRestrictionField.SetValue(bill, localPawn);
                        }),
                        payload = pawn2
                    };
                    yield return dropdownMenuElement;
                }
                else
                {
                    Pawn localPawn = pawn2;
                    dropdownMenuElement = new Widgets.DropdownMenuElement<Pawn>
                    {
                        option = new FloatMenuOption(pawn2.LabelShortCap, delegate
                        {
                            pawnRestrictionField.SetValue(bill, localPawn);
                        }),
                        payload = pawn2
                    };
                    yield return dropdownMenuElement;
                }
            }
        }
        
        private void CreateBillsWithMaterials()
        {
            if (selectedRecipe == null)
            {
                Messages.Message("ProductionFlow.NoRecipeSelected".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
            // Track workbenches that have been used for bills
            HashSet<Thing> usedWorkbenches = new HashSet<Thing>();
            int materialBillsCreated = 0;
            
            // Get all available workbenches for the main recipe
            List<Thing> availableWorkbenches = GetAvailableWorkbenches();
            
            // Priority order for main recipe:
            // 1. Selected workbenches
            // 2. Workbenches already used for materials
            // 3. Other available workbenches
            List<Thing> prioritizedMainWorkbenches = new List<Thing>();
            
            // Add selected workbenches first
            foreach (Thing wb in selectedWorkbenches)
            {
                if (availableWorkbenches.Contains(wb) && !prioritizedMainWorkbenches.Contains(wb))
                {
                    prioritizedMainWorkbenches.Add(wb);
                }
            }
            
            // Process materials first to populate usedWorkbenches
            if (selectedRecipe.ingredients != null && selectedRecipe.ingredients.Count > 0)
            {
                // Calculate how many items we need to produce
                int mainRecipeCount = 1;
                if (selectedRepeatMode == BillRepeatModeDefOf.RepeatCount)
                {
                    mainRecipeCount = quantity;
                }
                else if (selectedRepeatMode == BillRepeatModeDefOf.TargetCount)
                {
                    // For TargetCount, we need to estimate - use targetCount as base
                    mainRecipeCount = targetCount;
                }
                
                // Process each ingredient
                foreach (IngredientCount ingredient in selectedRecipe.ingredients)
                {
                    if (ingredient.filter == null)
                        continue;
                    
                    // Find recipes that can produce items matching this ingredient filter
                    // Use cached version with duplicate filtering to match hierarchy display logic
                    List<RecipeDef> materialRecipes = FindRecipesForIngredientCached(ingredient.filter);
                    
                    if (materialRecipes.Count > 0)
                    {
                        // Calculate how much material is needed
                        int materialCount = CalculateMaterialNeeded(ingredient, mainRecipeCount);
                        
                        // Find workbenches for material recipes with priority:
                        // 1. Selected workbenches first
                        // 2. Workbenches already used for materials
                        // 3. Other available workbenches
                        foreach (RecipeDef materialRecipe in materialRecipes)
                        {
                            List<Thing> materialWorkbenches = GetWorkbenchesForRecipeWithPriority(
                                materialRecipe, 
                                selectedWorkbenches, 
                                usedWorkbenches
                            );
                            
                            if (materialWorkbenches.Count > 0)
                            {
                                // Create bills on prioritized workbenches, minimizing number used
                                int created = CreateMaterialBillsOnWorkbenches(
                                    materialRecipe, 
                                    materialWorkbenches, 
                                    materialCount, 
                                    usedWorkbenches
                                );
                                materialBillsCreated += created;
                                
                                // Add used workbenches to prioritized list for main recipe
                                // Only add if they can also produce the main recipe
                                foreach (Thing wb in materialWorkbenches)
                                {
                                    if (!usedWorkbenches.Contains(wb))
                                    {
                                        usedWorkbenches.Add(wb);
                                    }
                                    // Check if this workbench can also produce the main recipe
                                    if (wb.def.AllRecipes != null && wb.def.AllRecipes.Contains(selectedRecipe))
                                    {
                                        if (availableWorkbenches.Contains(wb) && !prioritizedMainWorkbenches.Contains(wb))
                                        {
                                            prioritizedMainWorkbenches.Add(wb);
                                        }
                                    }
                                }
                                
                                // Material found, break to next ingredient
                                break;
                            }
                        }
                    }
                }
            }
            
            // Add remaining available workbenches (not selected, not used)
            foreach (Thing wb in availableWorkbenches)
            {
                if (!prioritizedMainWorkbenches.Contains(wb))
                {
                    prioritizedMainWorkbenches.Add(wb);
                }
            }
            
            // Create bills for main recipe on prioritized workbenches
            // Priority: selected workbenches first, then workbenches already used for materials, then others
            List<Thing> workbenchesForMain = new List<Thing>();
            
            if (selectedWorkbenches.Count > 0)
            {
                // If workbenches are selected, prioritize them, but also include used workbenches that are selected
                // Order: selected workbenches that are also used > selected workbenches > used workbenches > others
                foreach (Thing wb in prioritizedMainWorkbenches)
                {
                    if (selectedWorkbenches.Contains(wb))
                    {
                        workbenchesForMain.Add(wb);
                    }
                }
                // Add remaining selected workbenches that weren't in prioritized list
                foreach (Thing wb in selectedWorkbenches)
                {
                    if (!workbenchesForMain.Contains(wb) && availableWorkbenches.Contains(wb))
                    {
                        workbenchesForMain.Add(wb);
                    }
                }
            }
            else
            {
                // If no workbenches selected, use prioritized list (selected first, then used, then others)
                workbenchesForMain = prioritizedMainWorkbenches;
            }
            
            int mainBillsCreated = 0;
            foreach (Thing workbench in workbenchesForMain)
            {
                if (workbench is IBillGiver billGiver)
                {
                    if (workbench.def.AllRecipes != null && workbench.def.AllRecipes.Contains(selectedRecipe))
                    {
                        ClearCompletedBillsFromWorkbench(billGiver);
                        
                        Bill bill = selectedRecipe.MakeNewBill();
                        
                        if (bill is Bill_Production billProduction)
                        {
                            billProduction.SetStoreMode(BillStoreModeDefOf.BestStockpile);
                            billProduction.repeatMode = selectedRepeatMode;
                            
                            if (selectedRepeatMode == BillRepeatModeDefOf.RepeatCount)
                            {
                                billProduction.repeatCount = quantity;
                            }
                            else if (selectedRepeatMode == BillRepeatModeDefOf.TargetCount)
                            {
                                billProduction.targetCount = targetCount;
                            }
                            
                            if (selectedQuality.HasValue && selectedRecipe.workSkill != null)
                            {
                                billProduction.qualityRange = new QualityRange(selectedQuality.Value, selectedQuality.Value);
                            }
                            
                            if (materialFilter != null && billProduction.ingredientFilter != null)
                            {
                                billProduction.ingredientFilter.CopyAllowancesFrom(materialFilter);
                            }
                        }
                        
                        AddBillAtStart(billGiver, bill);
                        mainBillsCreated++;
                    }
                }
            }
            
            if (mainBillsCreated > 0 || materialBillsCreated > 0)
            {
                Messages.Message("ProductionFlow.BillsCreatedWithMaterials".Translate(mainBillsCreated, materialBillsCreated), MessageTypeDefOf.PositiveEvent);
                if (selectedWorkbenches.Count > 0)
                {
                    selectedWorkbenches.Clear();
                }
            }
            else
            {
                Messages.Message("ProductionFlow.NoBillsCreated".Translate(), MessageTypeDefOf.RejectInput);
            }
        }
        
        private List<RecipeDef> FindRecipesForIngredient(ThingFilter filter)
        {
            List<RecipeDef> recipes = new List<RecipeDef>();
            
            if (Current.Game == null || filter == null)
                return recipes;
            
            HashSet<RecipeDef> uniqueRecipes = new HashSet<RecipeDef>();
            
            // Search through all recipes to find those that produce items matching the filter
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null || map.listerThings == null)
                    continue;
                
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing is IBillGiver && thing.def.building != null && thing.Spawned)
                    {
                        if (thing.def.AllRecipes != null)
                        {
                            foreach (RecipeDef recipe in thing.def.AllRecipes)
                            {
                                if (recipe.products != null && recipe.products.Count > 0)
                                {
                                    // Check if any product matches the ingredient filter
                                    foreach (var product in recipe.products)
                                    {
                                        if (product.thingDef != null && filter.Allows(product.thingDef))
                                        {
                                            uniqueRecipes.Add(recipe);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            return uniqueRecipes.ToList();
        }
        
        private int CalculateMaterialNeeded(IngredientCount ingredient, int mainRecipeCount)
        {
            // Calculate base count needed per recipe
            int baseCount = (int)ingredient.GetBaseCount();
            
            // Multiply by number of main recipes
            int totalNeeded = baseCount * mainRecipeCount;
            
            // Round up to account for partial production
            return totalNeeded;
        }
        
        private List<Thing> GetWorkbenchesForRecipeWithPriority(
            RecipeDef recipe, 
            HashSet<Thing> selectedWorkbenches, 
            HashSet<Thing> usedWorkbenches)
        {
            List<Thing> prioritized = new List<Thing>();
            List<Thing> other = new List<Thing>();
            
            if (Current.Game == null)
                return prioritized;
            
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null || map.listerThings == null)
                    continue;
                
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing is IBillGiver && thing.def.building != null && thing.Spawned)
                    {
                        if (thing.def.AllRecipes != null && thing.def.AllRecipes.Contains(recipe))
                        {
                            if (selectedWorkbenches.Contains(thing))
                            {
                                // Priority 1: Selected workbenches
                                if (!prioritized.Contains(thing))
                                {
                                    prioritized.Insert(0, thing);
                                }
                            }
                            else if (usedWorkbenches.Contains(thing))
                            {
                                // Priority 2: Already used workbenches
                                if (!prioritized.Contains(thing))
                                {
                                    prioritized.Add(thing);
                                }
                            }
                            else
                            {
                                // Priority 3: Other available workbenches
                                if (!other.Contains(thing))
                                {
                                    other.Add(thing);
                                }
                            }
                        }
                    }
                }
            }
            
            // Combine: selected first, then used, then others
            prioritized.AddRange(other);
            return prioritized;
        }
        
        private int CreateMaterialBillsOnWorkbenches(
            RecipeDef materialRecipe, 
            List<Thing> workbenches, 
            int materialCount, 
            HashSet<Thing> usedWorkbenches)
        {
            // Get how many items the recipe produces per execution
            int itemsPerRecipe = 1;
            if (materialRecipe.products != null && materialRecipe.products.Count > 0)
            {
                var mainProduct = materialRecipe.products[0];
                if (mainProduct.count > 0)
                {
                    itemsPerRecipe = mainProduct.count;
                }
            }
            
            // Calculate how many recipe executions we need to produce enough material
            // Round up to ensure we have enough
            int recipesNeeded = (int)Math.Ceiling((double)materialCount / itemsPerRecipe);
            
            // Minimize number of workbenches by grouping tasks
            // Try to use as few workbenches as possible by putting all tasks on first workbench if possible
            
            int remainingRecipes = recipesNeeded;
            int billsCreated = 0;
            
            foreach (Thing workbench in workbenches)
            {
                if (remainingRecipes <= 0)
                    break;
                
                if (workbench is IBillGiver billGiver)
                {
                    if (workbench.def.AllRecipes != null && workbench.def.AllRecipes.Contains(materialRecipe))
                    {
                        ClearCompletedBillsFromWorkbench(billGiver);
                        
                        Bill bill = materialRecipe.MakeNewBill();
                        
                        if (bill is Bill_Production billProduction)
                        {
                            billProduction.SetStoreMode(BillStoreModeDefOf.BestStockpile);
                            billProduction.repeatMode = BillRepeatModeDefOf.RepeatCount;
                            
                            // Put all remaining recipes on this workbench to minimize number of workbenches used
                            billProduction.repeatCount = remainingRecipes;
                            remainingRecipes = 0;
                        }
                        
                        AddBillAtStart(billGiver, bill);
                        usedWorkbenches.Add(workbench);
                        billsCreated++;
                        
                        if (remainingRecipes <= 0)
                            break;
                    }
                }
            }
            
            return billsCreated;
        }
        
        private List<RecipeNode> GetRelatedRecipesHierarchy()
        {
            List<RecipeNode> rootNodes = new List<RecipeNode>();
            
            if (selectedRecipe == null || selectedRecipe.ingredients == null || selectedRecipe.ingredients.Count == 0)
                return rootNodes;
            
            // Clear cache if recipe changed
            if (cachedRecipeForFilter != selectedRecipe)
            {
                recipeCacheByFilterSummary.Clear();
                cachedRecipeForFilter = selectedRecipe;
            }
            
            HashSet<RecipeDef> rootRecipes = new HashSet<RecipeDef>();
            
            // For each ingredient in the selected recipe, find recipes that produce matching materials
            foreach (IngredientCount ingredient in selectedRecipe.ingredients)
            {
                if (ingredient.filter == null)
                    continue;
                
                List<RecipeDef> recipesForIngredient = FindRecipesForIngredientCached(ingredient.filter);
                foreach (RecipeDef recipe in recipesForIngredient)
                {
                    rootRecipes.Add(recipe);
                }
            }
            
            // Filter duplicates from root recipes
            List<RecipeDef> filteredRootRecipes = FilterDuplicateRecipes(rootRecipes.ToList());
            
            // Build tree for each root recipe (only first level, children built on demand when expanded)
            foreach (RecipeDef rootRecipe in filteredRootRecipes.OrderBy(r => r.label))
            {
                HashSet<RecipeDef> branchRecipes = new HashSet<RecipeDef>(); // Track recipes in current branch to avoid cycles
                RecipeNode node = BuildRecipeNode(rootRecipe, branchRecipes, 0);
                if (node != null)
                {
                    rootNodes.Add(node);
                }
            }
            
            return rootNodes;
        }
        
        private RecipeNode BuildRecipeNode(RecipeDef recipe, HashSet<RecipeDef> branchRecipes, int depth)
        {
            // Limit depth to avoid performance issues
            if (depth >= MAX_RECIPE_HIERARCHY_DEPTH)
                return null;
            
            // Avoid cycles: don't process if this recipe is already in the current branch
            if (branchRecipes.Contains(recipe))
                return null;
            
            branchRecipes.Add(recipe);
            RecipeNode node = new RecipeNode(recipe);
            
            // Check if this recipe has ingredients that can be produced by other recipes
            if (recipe.ingredients != null && recipe.ingredients.Count > 0 && depth < MAX_RECIPE_HIERARCHY_DEPTH - 1)
            {
                foreach (IngredientCount ingredient in recipe.ingredients)
                {
                    if (ingredient.filter == null)
                        continue;
                    
                    List<RecipeDef> childRecipes = FindRecipesForIngredientCached(ingredient.filter);
                    
                    foreach (RecipeDef childRecipe in childRecipes)
                    {
                        // Avoid cycles: don't add if it's the same recipe
                        if (childRecipe != recipe)
                        {
                            // Create a new branch set for each child to allow same recipe in different branches
                            HashSet<RecipeDef> childBranch = new HashSet<RecipeDef>(branchRecipes);
                            RecipeNode childNode = BuildRecipeNode(childRecipe, childBranch, depth + 1);
                            if (childNode != null)
                            {
                                node.Children.Add(childNode);
                            }
                        }
                    }
                }
            }
            
            return node;
        }
        
        private List<RecipeDef> FindRecipesForIngredientCached(ThingFilter filter)
        {
            if (filter == null)
                return new List<RecipeDef>();
            
            // Use filter summary as cache key
            string filterKey = filter.Summary;
            if (string.IsNullOrEmpty(filterKey))
                filterKey = "empty";
            
            // Check cache first
            if (recipeCacheByFilterSummary.ContainsKey(filterKey))
            {
                return recipeCacheByFilterSummary[filterKey];
            }
            
            // Use GetAllRecipes() for better performance
            List<RecipeDef> allRecipes = GetAllRecipes();
            HashSet<RecipeDef> uniqueRecipes = new HashSet<RecipeDef>();
            
            foreach (RecipeDef recipe in allRecipes)
            {
                if (recipe.products == null || recipe.products.Count == 0)
                    continue;
                
                // Check if any product matches the ingredient filter
                foreach (var product in recipe.products)
                {
                    if (product.thingDef != null && filter.Allows(product.thingDef))
                    {
                        uniqueRecipes.Add(recipe);
                        break;
                    }
                }
            }
            
            // Filter duplicates: keep only recipe with smallest count for each product type
            List<RecipeDef> filteredRecipes = FilterDuplicateRecipes(uniqueRecipes.ToList());
            recipeCacheByFilterSummary[filterKey] = filteredRecipes;
            return filteredRecipes;
        }
        
        private List<RecipeDef> FilterDuplicateRecipes(List<RecipeDef> recipes)
        {
            // Group recipes by their main product (thingDef)
            // For each product type, keep only the recipe with smallest count
            Dictionary<ThingDef, RecipeDef> bestRecipes = new Dictionary<ThingDef, RecipeDef>();
            
            foreach (RecipeDef recipe in recipes)
            {
                if (recipe.products == null || recipe.products.Count == 0)
                    continue;
                
                // Get main product (first product)
                var mainProduct = recipe.products[0];
                if (mainProduct.thingDef == null)
                    continue;
                
                ThingDef productDef = mainProduct.thingDef;
                int productCount = mainProduct.count;
                
                if (!bestRecipes.ContainsKey(productDef))
                {
                    // First recipe for this product
                    bestRecipes[productDef] = recipe;
                }
                else
                {
                    // Compare with existing recipe - keep the one with smaller count
                    RecipeDef existingRecipe = bestRecipes[productDef];
                    int existingCount = existingRecipe.products[0].count;
                    
                    if (productCount < existingCount)
                    {
                        bestRecipes[productDef] = recipe;
                    }
                }
            }
            
            return bestRecipes.Values.ToList();
        }
        
        private void DrawRelatedRecipesPanel(Rect rect, float rowHeight)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            Widgets.Label(headerRect, "ProductionFlow.RelatedRecipes".Translate());
            
            Rect scrollRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, relatedRecipesScrollHeight);
            
            Widgets.BeginScrollView(scrollRect, ref relatedRecipesScrollPosition, viewRect);
            
            if (selectedRecipe == null)
            {
                Rect noRecipeRect = new Rect(0f, 0f, viewRect.width, rowHeight);
                Widgets.Label(noRecipeRect, "ProductionFlow.SelectRecipeForRelatedRecipes".Translate());
                relatedRecipesScrollHeight = rowHeight;
            }
            else
            {
                List<RecipeNode> recipeHierarchy = GetRelatedRecipesHierarchy();
                
                if (recipeHierarchy.Count == 0)
                {
                    Rect noRelatedRecipesRect = new Rect(0f, 0f, viewRect.width, rowHeight);
                    Widgets.Label(noRelatedRecipesRect, "ProductionFlow.NoRelatedRecipes".Translate());
                    relatedRecipesScrollHeight = rowHeight;
                }
                else
                {
                    float yPos = 0f;
                    float recipeRowHeight = rowHeight + 2f;
                    
                    foreach (RecipeNode rootNode in recipeHierarchy)
                    {
                        yPos = DrawRecipeNode(viewRect, rootNode, 0, yPos, recipeRowHeight, scrollRect);
                    }
                    relatedRecipesScrollHeight = yPos;
                }
            }
            
            Widgets.EndScrollView();
        }
        
        private float DrawRecipeNode(Rect viewRect, RecipeNode node, int depth, float yPos, float rowHeight, Rect scrollRect)
        {
            Rect recipeRect = new Rect(0f, yPos, viewRect.width, rowHeight - 2f);
            
            if (yPos - relatedRecipesScrollPosition.y + rowHeight >= 0f && yPos - relatedRecipesScrollPosition.y <= scrollRect.height)
            {
                DrawRelatedRecipeRow(recipeRect, node.Recipe, depth, node.Children.Count > 0);
            }
            
            yPos += rowHeight;
            
            // Draw children if expanded
            bool isExpanded = expandedRelatedRecipes.Contains(node.Recipe);
            if (isExpanded && node.Children.Count > 0)
            {
                foreach (RecipeNode childNode in node.Children.OrderBy(n => n.Recipe.label))
                {
                    yPos = DrawRecipeNode(viewRect, childNode, depth + 1, yPos, rowHeight, scrollRect);
                }
            }
            
            return yPos;
        }
        
        private void DrawRelatedRecipeRow(Rect rect, RecipeDef recipe, int depth, bool hasChildren)
        {
            float indentPerLevel = 20f;
            float indent = depth * indentPerLevel;
            float expandButtonSize = rect.height * 0.7f;
            float iconOffset = indent + 5f;
            
            // Draw expand/collapse button if has children
            if (hasChildren)
            {
                bool isExpanded = expandedRelatedRecipes.Contains(recipe);
                Rect expandButtonRect = new Rect(rect.x + indent + 2f, rect.y + (rect.height - expandButtonSize) / 2f, expandButtonSize, expandButtonSize);
                
                if (Widgets.ButtonInvisible(expandButtonRect))
                {
                    if (isExpanded)
                    {
                        expandedRelatedRecipes.Remove(recipe);
                    }
                    else
                    {
                        expandedRelatedRecipes.Add(recipe);
                    }
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }
                
                // Draw expand/collapse icon
                Texture2D expandIcon = isExpanded ? TexButton.Collapse : TexButton.Reveal;
                if (expandIcon != null)
                {
                    GUI.DrawTexture(expandButtonRect, expandIcon);
                }
                
                iconOffset += expandButtonSize + 5f;
            }
            else
            {
                iconOffset += 5f;
            }
            
            // Draw recipe icon
            Texture2D icon = null;
            if (recipe.products != null && recipe.products.Count > 0)
            {
                var firstProduct = recipe.products[0];
                if (firstProduct.thingDef != null)
                {
                    icon = firstProduct.thingDef.uiIcon;
                }
            }
            
            float iconSize = rect.height * 0.8f;
            
            if (icon != null)
            {
                Rect iconRect = new Rect(rect.x + iconOffset, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon);
                iconOffset += iconSize + 5f;
            }
            
            Rect labelRect = new Rect(rect.x + iconOffset, rect.y, rect.width - iconOffset - 5f, rect.height);
            string label = recipe.LabelCap;
            if (recipe.products != null && recipe.products.Count > 0)
            {
                var firstProduct = recipe.products[0];
                if (firstProduct.thingDef != null)
                {
                    label = firstProduct.thingDef.LabelCap;
                    if (firstProduct.count > 1)
                    {
                        label += " (" + firstProduct.count + " шт)";
                    }
                }
            }
            Widgets.Label(labelRect, label);
            
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, recipe.description);
            }
        }
    }
}


