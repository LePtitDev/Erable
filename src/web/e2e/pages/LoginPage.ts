import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The login screen of a server (F14) and the avatar menu that ends the session. */
export class LoginPage {
  readonly form: Locator;
  readonly login: Locator;
  readonly password: Locator;
  readonly submit: Locator;
  readonly error: Locator;

  constructor(readonly page: Page) {
    this.form = page.getByRole("form", { name: t("auth.title") });
    this.login = this.form.getByLabel(t("auth.login"));
    this.password = this.form.getByLabel(t("auth.password"));
    this.submit = this.form.getByRole("button", { name: t("auth.submit") });
    this.error = this.form.getByRole("alert");
  }

  async signIn(login: string, password: string) {
    await this.login.fill(login);
    await this.password.fill(password);
    await this.submit.click();
  }

  /** The avatar of the signed-in user (top bar of a project). */
  avatar(name: string): Locator {
    return this.page.getByRole("button", { name: t("auth.userMenu", { name }) });
  }

  async signOut(name: string) {
    await this.avatar(name).click();
    await this.page.getByRole("menuitem", { name: t("auth.logout") }).click();
  }
}
