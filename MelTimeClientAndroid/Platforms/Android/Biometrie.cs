using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;

namespace MelTimeClientAndroid;

/// <summary>
/// Empreinte digitale, par BiometricPrompt d'AndroidX.
///
/// Android affiche lui-meme sa boite de dialogue : l'application ne recoit
/// jamais l'image de l'empreinte, seulement un succes ou un abandon. C'est
/// le seul moyen sur Android de garantir le secret : rien a stocker, rien
/// a comparer.
/// </summary>
public static class Biometrie
{
    /// <summary>Vrai si le telephone a une empreinte enregistree et du materiel utilisable.</summary>
    public static bool EstDisponible
    {
        get
        {
            try
            {
                var gestionnaire = BiometricManager.From(Platform.AppContext);

                var resultat = gestionnaire.CanAuthenticate(
                    BiometricManager.Authenticators.BiometricWeak
                    | BiometricManager.Authenticators.BiometricStrong);

                return resultat == BiometricManager.BiometricSuccess;
            }
            catch
            {
                // Certains appareils anciens levent une exception au lieu de repondre.
                return false;
            }
        }
    }

    /// <summary>
    /// Ouvre la boite de dialogue d'empreinte et attend la reponse.
    /// Retourne vrai seulement si l'empreinte est reconnue.
    /// </summary>
    public static Task<bool> AuthentifierAsync(string titre, string sousTitre)
    {
        var tache = new TaskCompletionSource<bool>();

        if (Platform.CurrentActivity is not FragmentActivity activite)
        {
            tache.SetResult(false);
            return tache.Task;
        }

        try
        {
            var executeur = ContextCompat.GetMainExecutor(activite);
            var prompt = new BiometricPrompt(activite, executeur, new Rappel(tache));

            var informations = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle(titre)
                .SetSubtitle(sousTitre)
                .SetNegativeButtonText("Annuler")
                .SetAllowedAuthenticators(
                    BiometricManager.Authenticators.BiometricWeak
                    | BiometricManager.Authenticators.BiometricStrong)
                .Build();

            prompt.Authenticate(informations);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Empreinte impossible : " + ex.Message);
            tache.TrySetResult(false);
        }

        return tache.Task;
    }

    /// <summary>Retour d'Android : une seule reponse compte, les suivantes sont ignorees.</summary>
    private sealed class Rappel : BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<bool> _tache;

        public Rappel(TaskCompletionSource<bool> tache) => _tache = tache;

        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? resultat)
            => _tache.TrySetResult(true);

        public override void OnAuthenticationError(int codeErreur, Java.Lang.ICharSequence? message)
            => _tache.TrySetResult(false);

        public override void OnAuthenticationFailed()
        {
            // Empreinte non reconnue : Android laisse la boite ouverte pour un nouvel essai.
            // On ne termine donc pas la tache ici.
        }
    }
}
