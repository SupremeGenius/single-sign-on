import {Component} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from '@angular/forms';
import {Title} from '@angular/platform-browser';
import {BaseComponent} from '@vcpsh/sso-client-lib';
import {AccountService} from '../../services/account.service';

@Component({
  selector: 'app-forgot',
  templateUrl: './forgot.component.html',
  styleUrls: ['./forgot.component.scss']
})
export class ForgotComponent extends BaseComponent {
  public Form: FormGroup;
  public Success = false;

  constructor(
    fb: FormBuilder,
    title: Title,
    private _service: AccountService,
  ) {
    super();
    title.setTitle('Forgot Password - vcp.sh');
    this.Form = fb.group({
      email: ['', [Validators.email, Validators.required]]
    });
  }

  public onResetClick() {
    if (this.Form.valid) {
      this._service.forgot(this.Form.value).then(val => {
        if (!val) {
          this.Form.controls['email'].setErrors({ 'error': 'Unbekannte Email'});
        } else {
          this.Success = true;
        }
      });
    } else {
      this.Form.markAsTouched();
    }
  }
}
