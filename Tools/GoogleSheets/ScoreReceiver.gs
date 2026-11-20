// Reçoit les mesures de performance de chaque partie envoyées par le jeu (MetricsReporter) et les ajoute en
// ligne dans l'onglet « Mesures » de la feuille. Les colonnes suivent les champs reçus : un champ nouveau
// ajoute sa colonne, sans avoir à modifier ce script. Les champs commençant par « _ » servent au script et
// n'ont pas de colonne. Une ligne déjà reçue (même session, même partie : tentative répétée par le jeu) est ignorée.
// La réponse à la question ouverte (champ _remarque) arrive à part et complète la ligne de sa partie.
// Affecte aussi chaque session au mode adaptatif ou fixe (mode Balanced du DistractionDirector) : onglet
// « Affectations », une ligne par session.
//
// Installation : dans la feuille, Extensions > Apps Script, coller ce fichier, enregistrer, puis
// Déployer > Gérer les déploiements > (crayon) Modifier > Version : « Nouvelle version » > Déployer
// (Exécuter en tant que : moi, Qui a accès : Tout le monde). L'URL /exec ne change pas.
// Enregistrer seul ne suffit pas : l'URL /exec sert la version déployée. « Nouveau déploiement » donnerait une
// autre URL, que le jeu n'utilise pas.
// Vérification : ouvrir l'URL /exec dans un navigateur doit répondre « ScoreReceiver OK » suivi de SCRIPT_VERSION.

// Affichée en ouvrant l'URL /exec, sans rien écrire : montre quelle version est réellement déployée.
const SCRIPT_VERSION = '0.2.1';
const SPREADSHEET_ID ='YOUR_SPREADSHEET_ID';
const SHEET_NAME = 'Mesures';
const ASSIGN_SHEET_NAME = 'Affectations';
// Une session non terminée compte encore dans l'équilibre pendant ce temps (minutes) : elle est sans doute en
// cours (une session dure 6 à 8 min). Au-delà, elle est considérée comme abandonnée.
const IN_PROGRESS_MINUTES = 30;
// Colonnes de l'onglet « Affectations ».
const ASSIGN_HEADERS = ['date', 'session', 'participant', 'mode', 'terminee'];

function doPost(e) {
  const data = JSON.parse(e.postData.contents);

  // Plusieurs parties peuvent finir en même temps : une écriture à la fois.
  const lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    const spreadsheet = SpreadsheetApp.openById(SPREADSHEET_ID);
    const sheet = spreadsheet.getSheetByName(SHEET_NAME) || spreadsheet.insertSheet(SHEET_NAME);

    const headers = sheet.getLastRow() === 0 ? ['date'] : sheet.getRange(1, 1, 1, sheet.getLastColumn()).getValues()[0];
    let added = sheet.getLastRow() === 0;
    Object.keys(data).forEach(key => {
      if (!key.startsWith('_') && !headers.includes(key)) {
        headers.push(key);
        added = true;
      }
    });
    if (added) {
      sheet.getRange(1, 1, 1, headers.length).setValues([headers]).setFontWeight('bold');
      sheet.setFrozenRows(1);
    }

    const session = String(data.session || '');

    if (data._remarque !== undefined) {
      saveRemark(sheet, headers, session, data);
    } else {
      // Valeur absente ou null (mesure non calculable) : cellule vide.
      if (!session || !findRow(sheet, headers, session, data.partie))
        sheet.appendRow(headers.map(h => h === 'date' ? new Date() : h === 'session' ? asText(session) : (data[h] === undefined || data[h] === null ? '' : cell(data[h]))));

      // Dernière partie de la session : la session est terminée.
      if (session && data._derniere_partie === 1)
        markFinished(spreadsheet, session);
    }
  } finally {
    lock.releaseLock();
  }

  return ContentService.createTextOutput(JSON.stringify({ ok: true })).setMimeType(ContentService.MimeType.JSON);
}

// Texte commençant par = + - @ (réponse libre « - les lumières… ») : précédé d'une apostrophe, sinon la feuille le
// lirait comme une formule.
function cell(v) {
  return typeof v === 'string' && /^[=+\-@]/.test(v) ? "'" + v : v;
}

// Identifiant forcé en texte : sinon la feuille lirait « 00123456 » ou « 1e234567 » comme des nombres.
function asText(v) {
  return "'" + v;
}

// Numéro de ligne (à partir de 2) de cette partie de cette session, 0 si elle n'y est pas.
function findRow(sheet, headers, session, partie) {
  const s = headers.indexOf('session');
  const p = headers.indexOf('partie');
  if (s < 0 || p < 0 || sheet.getLastRow() < 2)
    return 0;
  const rows = sheet.getRange(2, 1, sheet.getLastRow() - 1, headers.length).getValues();
  const i = rows.findIndex(r => String(r[s]) === session && String(r[p]) === String(partie));
  return i < 0 ? 0 : i + 2;
}

// Réponse à la question ouverte, sur la ligne de sa partie. Si cette ligne n'est jamais arrivée, la réponse est gardée
// sur une ligne à part.
function saveRemark(sheet, headers, session, data) {
  ['session', 'partie', 'remarque_libre'].forEach(h => {
    if (!headers.includes(h)) {
      headers.push(h);
      sheet.getRange(1, 1, 1, headers.length).setValues([headers]).setFontWeight('bold');
    }
  });
  const row = findRow(sheet, headers, session, data.partie);
  if (row)
    sheet.getRange(row, headers.indexOf('remarque_libre') + 1).setValue(cell(data._remarque));
  else
    sheet.appendRow(headers.map(h => h === 'date' ? new Date() : h === 'session' ? asText(session) : h === 'partie' ? data.partie : h === 'remarque_libre' ? cell(data._remarque) : ''));
}

// ?action=assign&session=…&participant=… : mode de la session (« adaptatif » ou « fixe »). Sans paramètre, ouvrir
// l'URL /exec dans un navigateur permet de vérifier que le déploiement répond.
function doGet(e) {
  if (e && e.parameter && e.parameter.action === 'assign') {
    const result = assign(String(e.parameter.session || ''), String(e.parameter.participant || ''));
    return ContentService.createTextOutput(JSON.stringify(result)).setMimeType(ContentService.MimeType.JSON);
  }
  return ContentService.createTextOutput('ScoreReceiver OK ' + SCRIPT_VERSION);
}

// Mode qui compte le moins de sessions terminées ou en cours ; à égalité, au hasard. Une session déjà affectée
// (demande répétée) garde son mode.
function assign(session, participant) {
  const lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    const sheet = assignSheet(SpreadsheetApp.openById(SPREADSHEET_ID));
    const rows = sheet.getLastRow() > 1 ? sheet.getRange(2, 1, sheet.getLastRow() - 1, ASSIGN_HEADERS.length).getValues() : [];

    const existing = rows.find(r => session && String(r[1]) === session);
    if (existing)
      return { mode: existing[3] };

    const counts = { adaptatif: 0, fixe: 0 };
    const cutoff = Date.now() - IN_PROGRESS_MINUTES * 60000;
    rows.forEach(r => {
      if (r[3] in counts && (r[4] !== '' || new Date(r[0]).getTime() > cutoff))
        counts[r[3]]++;
    });
    const mode = counts.adaptatif < counts.fixe ? 'adaptatif'
               : counts.fixe < counts.adaptatif ? 'fixe'
               : (Math.random() < 0.5 ? 'adaptatif' : 'fixe');
    sheet.appendRow([new Date(), asText(session), participant, mode, '']);
    return { mode: mode, adaptatif: counts.adaptatif, fixe: counts.fixe };
  } finally {
    lock.releaseLock();
  }
}

function markFinished(spreadsheet, session) {
  const sheet = assignSheet(spreadsheet);
  if (sheet.getLastRow() < 2)
    return;
  const sessions = sheet.getRange(2, 2, sheet.getLastRow() - 1, 1).getValues();
  const i = sessions.findIndex(r => String(r[0]) === session);
  if (i >= 0)
    sheet.getRange(i + 2, 5).setValue(new Date());
}

function assignSheet(spreadsheet) {
  let sheet = spreadsheet.getSheetByName(ASSIGN_SHEET_NAME);
  if (!sheet) {
    sheet = spreadsheet.insertSheet(ASSIGN_SHEET_NAME);
  }
  // Onglet neuf, ou vidé à la main : les titres sont (re)mis en première ligne, au-dessus des lignes existantes.
  if (sheet.getRange(1, 1).getValue() !== ASSIGN_HEADERS[0]) {
    if (sheet.getLastRow() > 0)
      sheet.insertRowBefore(1);
    sheet.getRange(1, 1, 1, ASSIGN_HEADERS.length).setValues([ASSIGN_HEADERS]).setFontWeight('bold');
    sheet.setFrozenRows(1);
  }
  return sheet;
}
