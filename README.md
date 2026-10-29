# SlotHub: Többszolgáltatós időpontfoglaló rendszer fejlesztése

Ez egy modern, ASP.NET Core MVC alapú időpontfoglaló rendszer, amely lehetővé teszi a felhasználók számára, hogy online regisztráljanak és különböző szolgáltatásokra időpontot foglaljanak.

## Főbb funkciók
- **Felhasználókezelés:** Regisztráció és biztonságos bejelentkezés (BCrypt jelszó-hasheléssel).
- **Időpontfoglalás:** Rugalmas keresés a kategóriák között és foglalási folyamat (folyamatban).
- **Profilkezelés:** Felhasználói adatok tárolása és kezelése.

## Technológiai stack
- **Backend:** C# / ASP.NET Core 8.0+ MVC
- **Adatbázis:** MySQL / MariaDB (XAMPP környezetben)
- **ORM:** Entity Framework Core (Code-First megközelítés)
- **Frontend:** Bootstrap 5, Razor Pages, CSS3
- **Biztonság:** BCrypt.Net-Next a jelszavak titkosításához

## Előfeltételek (Requirements)
A futtatáshoz a következő szoftverekre lesz szükséged:
1. **.NET SDK 8.0+** vagy újabb
2. **Visual Studio 2022** (ASP.NET és Web development csomaggal)
3. **XAMPP** (Apache és MySQL modulok futtatásához)

## Telepítés és beállítás (Install Guide)

1. **Adatbázis elindítása:**
   - Indítsd el a XAMPP Control Panel-t.
   - Indítsd el a **MySQL** modult.

2. **Konfiguráció:**
   - Nyisd meg az `appsettings.json` fájlt.
   - Ellenőrizd a `ConnectionStrings` részt (alapértelmezett port: 3306).

3. **Adatbázis létrehozása (Migrations):**
   - Nyisd meg a Visual Studio-ban a *Package Manager Console*-t.
   - Futtasd a következő parancsot:
     ```powershell
     Update-Database
     ```

4. **Futtatás:**
   - Nyomd meg az `F5` billentyűt vagy kattints a futtatás gombra a Visual Studio-ban.

## Licenc
Ez a projekt szakdolgozat keretében készült. Minden jog fenntartva.
