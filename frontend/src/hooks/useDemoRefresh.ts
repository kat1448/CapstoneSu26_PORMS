import { useEffect, useState } from "react";
import { subscribeToDemoData } from "../mock/demoData";

export function useDemoRefresh() {
  const [version, setVersion] = useState(0);

  useEffect(() => {
    return subscribeToDemoData(() => setVersion((value) => value + 1));
  }, []);

  return version;
}
