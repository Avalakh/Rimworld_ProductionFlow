using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProductionFlow
{
    public class MainTabWindow_ProductionFlow : MainTabWindow
    {
        // Scroll positions for each panel
        private Vector2 recipesScrollPosition = Vector2.zero;
        private Vector2 workbenchesScrollPosition = Vector2.zero;
        private Vector2 pawnsScrollPosition = Vector2.zero;
        private Vector2 recipeInfoScrollPosition = Vector2.zero;
        private Vector2 workbenchBillsScrollPosition = Vector2.zero;
        private Vector2 materialsScrollPosition = Vector2.zero;
        
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
        
        // Bill creation settings
        private int quantity = 1;
        private int targetCount = 10;
        private QualityCategory? selectedQuality = null;
        private string quantityBuffer = "1";
        private string targetCountBuffer = "10";
        private BillRepeatModeDef selectedRepeatMode = BillRepeatModeDefOf.RepeatCount;
        
        // Recipe search
        private string recipeSearchText = "";
        

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
            
            // Column 4: Pawns (full height)
            Rect pawnsRect = new Rect(inRect.x + (columnWidth + margin) * 3, inRect.y, columnWidth, inRect.height);
            DrawPawnsPanel(pawnsRect, rowHeight);
            
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
            Widgets.Label(headerRect, "ProductionFlow.Workbenches".Translate());
            
            Rect scrollRect = new Rect(rect.x + 5f, rect.y + rowHeight + 15f, rect.width - 10f, rect.height - rowHeight - 20f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, workbenchesScrollHeight);
            
            Widgets.BeginScrollView(scrollRect, ref workbenchesScrollPosition, viewRect);
            
            List<Thing> workbenches = GetAvailableWorkbenches();
            
            if (workbenches.Count == 0)
            {
                Rect noWorkbenchesRect = new Rect(0f, 0f, viewRect.width, rowHeight);
                Widgets.Label(noWorkbenchesRect, "ProductionFlow.NoWorkbenches".Translate());
                workbenchesScrollHeight = rowHeight;
            }
            else
            {
                float yPos = 0f;
                float workbenchRowHeight = rowHeight * 1.5f;
                foreach (Thing workbench in workbenches)
                {
                    Rect workbenchRect = new Rect(0f, yPos, viewRect.width, workbenchRowHeight);
                    
                    if (yPos - workbenchesScrollPosition.y + workbenchRowHeight >= 0f && yPos - workbenchesScrollPosition.y <= scrollRect.height)
                    {
                        DrawWorkbenchRow(workbenchRect, workbench, rowHeight);
                    }
                    
                    yPos += workbenchRowHeight + 2f;
                }
                workbenchesScrollHeight = yPos;
            }
            
            Widgets.EndScrollView();
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
            
            float checkboxSize = rowHeight * 0.8f;
            Rect checkboxRect = new Rect(rect.x + 5f, rect.y + (rect.height - checkboxSize) / 2f, checkboxSize, checkboxSize);
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
            
            // Click area for selecting workbench for bills management
            Rect clickRect = new Rect(checkboxRect.xMax + 5f, rect.y, rect.width - checkboxRect.xMax - 5f, rect.height);
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
            
            // Draw workbench icon
            float iconSize = rowHeight * 0.8f;
            float iconOffset = checkboxSize + 10f;
            
            if (workbench.def.uiIcon != null)
            {
                Rect iconRect = new Rect(rect.x + iconOffset, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, workbench.def.uiIcon);
                iconOffset += iconSize + 5f;
            }
            
            Rect labelRect = new Rect(rect.x + iconOffset, rect.y, rect.width - iconOffset - 5f, rect.height);
            Widgets.Label(labelRect, workbench.LabelCap);
            
            // Show indicator if this workbench is selected for bills
            if (isSelectedForBills)
            {
                Rect indicatorRect = new Rect(rect.xMax - 15f, rect.y + (rect.height - 10f) / 2f, 10f, 10f);
                Widgets.DrawBoxSolid(indicatorRect, Color.green);
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
            
            // Create bills button (full width, directly under quantity/target)
            Rect buttonRect = new Rect(xPos, yPos, width, controlHeight);
            if (Widgets.ButtonText(buttonRect, "ProductionFlow.CreateBills".Translate()))
            {
                CreateBills();
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
                    Bill_Production tempBill = new Bill_Production(selectedRecipe);
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
            
            return uniqueRecipes.ToList();
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

        private void DrawWorkbenchBillsPanel(Rect rect, float rowHeight)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            Widgets.DrawBox(rect);
            
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rowHeight + 5f);
            Widgets.Label(headerRect, "ProductionFlow.WorkbenchBills".Translate());
            
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
                            billGiver.BillStack.AddBill(bill);
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
                                    billGiver.BillStack.AddBill(bill2);
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
                        Bill_Production bill = new Bill_Production(selectedRecipe);
                        bill.SetStoreMode(BillStoreModeDefOf.BestStockpile);
                        bill.repeatMode = selectedRepeatMode;
                        
                        if (selectedRepeatMode == BillRepeatModeDefOf.RepeatCount)
                        {
                        bill.repeatCount = quantity;
                        }
                        else if (selectedRepeatMode == BillRepeatModeDefOf.TargetCount)
                        {
                            bill.targetCount = targetCount;
                        }
                        // Forever mode doesn't need additional settings
                        
                        if (selectedQuality.HasValue && selectedRecipe.workSkill != null)
                        {
                            bill.qualityRange = new QualityRange(selectedQuality.Value, selectedQuality.Value);
                        }
                        
                        // Apply material filter if one is selected
                        if (materialFilter != null && bill.ingredientFilter != null)
                        {
                            // Copy the selected material filter to the bill's ingredient filter
                            // This sets the allowed materials for production
                            bill.ingredientFilter.CopyAllowancesFrom(materialFilter);
                        }
                        
                        billGiver.BillStack.AddBill(bill);
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
    }
}


