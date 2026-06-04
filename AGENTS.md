# Agent Coding Guidelines & Project Rules

This document outlines the strict guidelines and patterns that any AI coding agent or engineer must adhere to when working on this project.

---

## 1. Project Stack & Environment Rules
- **Framework**: .NET 8 WPF.
- **Language**: C# 12.
- **MVVM Library**: `CommunityToolkit.Mvvm` (using source generators for properties and commands).
- **ORM**: Entity Framework Core 8.0.
- **Database**: SQLite Local Provider (default configuration).

---

## 2. WPF MVVM Best Practices
- **No Code-Behind Logic**: View code-behind (e.g., `View.xaml.cs`) must only contain the call to `InitializeComponent()`. All business logic, states, validation messages, and actions must live in ViewModels.
- **Source Generator Bindings**: Use `[ObservableProperty]` and `[RelayCommand]` from the Community Toolkit. Do not write manual property notifications or command mappings unless absolutely required.
- **Dependency Injection**: 
  - Register all ViewModels and Views in [App.xaml.cs](file:///c:/Projects/University%20Library%20Management%20system/KicsitLibrary.Desktop/App.xaml.cs).
  - Avoid captive dependencies: resolve scoped services (like EF context databases) dynamically using `IDbContextFactory<KicsitLibraryDbContext>` or transient DI scopes when calling them from singleton services.

---

## 3. UI Styling & Theme Constraints
- **Navy Theme Design System**: The app uses a strict navy color system. Use colors defined in [Colors.xaml](file:///c:/Projects/University%20Library%20Management%20system/KicsitLibrary.Desktop/Themes/Colors.xaml) via static resources:
  - Primary color: `#1B2A47` (`PrimaryBrush`)
  - Slate secondary: `#2A3F6D` (`SecondaryBrush`)
  - Accent: `#005A9C` (`AccentBrush`)
  - Background: `#F4F6F9` (`BgBrush`)
  - Semantic: Success (`#10B981`), Danger (`#EF4444`), Warning (`#F59E0B`)
- **Card Styles**: Use `<Border Style="{StaticResource CardStyle}">` to wrap layouts. Never write raw shadow effects or borders manually.
- **WPF StringFormat Escape**: When binding string formats that start with curly braces in DataGrids or labels, prefix them with `{}` to avoid compiler error `MC1000`. Example:
  `StringFormat='{}{0:N0}'` instead of `StringFormat='{0:N0}'`.
- **ComboBox Tag Bindings**: Avoid using raw `System:Boolean` in ComboBox tag values as it triggers prefix compilation issues. Plain strings `"True"` and `"False"` work out-of-the-box.

---

## 4. Database & EF Core Rules
- **Enum Mapping**: All enums (e.g., `BookStatus`, `MemberType`, `ClearanceStatus`) must be stored as strings in SQLite. Register them inside `OnModelCreating` in the DbContext:
  `entity.Property(e => e.Status).HasConversion<string>();`
- **Soft Deleting**: The system uses soft deletes. Never delete rows directly from entity tables. Set `IsDeleted = true` and log the archive metadata details.
- **Missing Indexes**: Ensure that all foreign keys and frequently searched text columns (like accession numbers, reg numbers, names) have indices configured in the DbContext.

---

## 5. What NOT to Break
- **Navigation Router**: The navigation structure in [MainViewModel.cs](file:///c:/Projects/University%20Library%20Management%20system/KicsitLibrary.Desktop/ViewModels/MainViewModel.cs) uses scoped host resolution. Changing this signature can result in memory leaks or container exceptions.
- **Password Hasher**: The password seeder relies on PBKDF2 hashing in `IPasswordHasher`. Do not change the hashing iteration count or salt size, as it will break login for existing users.
- **Vector Code Generators**: The drawing barcode and QR vector classes in `KicsitLibrary.Desktop/Helpers` render to `DrawingImage` dynamically. Do not replace them with bitmap generators, as vector drawings are required for high-resolution library card printing.
