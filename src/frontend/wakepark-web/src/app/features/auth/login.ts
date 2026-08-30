import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService, InvalidCredentialsError } from '../../core/auth/auth.service';

@Component({
  selector: 'wp-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected async submit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    try {
      await this.auth.signIn(this.email(), this.password());

      const target = this.route.snapshot.queryParamMap.get('pokracovat') ?? '/parks';
      await this.router.navigateByUrl(target);
    } catch (error) {
      this.errorMessage.set(
        error instanceof InvalidCredentialsError
          ? error.message
          : 'Přihlášení se nepodařilo. Běží backend?',
      );
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
