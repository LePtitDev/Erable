import { useQuery } from "@tanstack/react-query";
import { apiInfoQuery } from "../../shared/api/queries";
import styles from "./HelloPage.module.css";

export function HelloPage() {
  const info = useQuery(apiInfoQuery);
  return (
    <main className={styles["page"]}>
      <h1>Erable</h1>
      {info.isSuccess && (
        <p>
          v{info.data.appVersion} · API {info.data.apiVersion} · {info.data.mode}
        </p>
      )}
      {info.isError && <p role="alert">{info.error.message}</p>}
    </main>
  );
}
